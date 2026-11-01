"""Private decision adapter: typed model outputs, never generated JSON."""
import argparse
import asyncio
import hmac
import json
import math
import os
from pathlib import Path
import sys
import threading
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def questions_from_chat(payload):
    schema = payload['response_format']['json_schema']['schema']
    messages = payload['messages']
    if not messages or not isinstance(messages[-1].get('content'), str):
        raise ValueError('Expected text decision state')
    state = messages[-1]['content']
    instructions = '\n'.join(m['content'] for m in messages if m['role'] == 'system')
    questions = {}
    for name, prop in schema['properties'].items():
        if prop.get('type') == 'string' and prop.get('enum'):
            descriptions = payload.get('decision_criteria', {})
            criteria = {value: descriptions.get(value, value) for value in prop['enum']}
            questions[name] = {'type':'choice', 'instructions':instructions, 'criteria':criteria}
        elif prop.get('type') == 'boolean':
            specific = ('Does the result retain ALL meaningful source content, including conditions, reasons, introductions and closing requests?'
                        if name == 'complete' else
                        'Does the result satisfy ALL the stated verification requirements without unrequested factual changes?')
            questions[name] = {'type':'noul', 'instructions':instructions+'\n'+specific}
        else:
            raise ValueError('This endpoint only supports choice and boolean decisions')
    if not 1 <= len(questions) <= 8 or len(state)+len(instructions) > 50000:
        raise ValueError('Decision is too large')
    return state, questions


def completion(payload, result):
    value = {}
    fallback = False
    for name, prop in payload['response_format']['json_schema']['schema']['properties'].items():
        answer = result['answers'][name]
        if prop.get('type') == 'boolean':
            probability = answer['noul']
            if not isinstance(probability, (int,float)) or not math.isfinite(probability) or not 0 <= probability <= 1:
                raise ValueError('Invalid decision probability')
            # Uncertainty rejects an edit rather than accepting an unverified change.
            value[name] = probability >= 0.8
            fallback = fallback or 0.2 < probability < 0.8
        else:
            selected = answer['choice']
            if selected not in prop['enum']:
                raise ValueError('Decision outside the allowed choices')
            value[name] = selected
            confidence=answer.get('confidence',0)
            if not isinstance(confidence,(int,float)) or not math.isfinite(confidence) or not 0<=confidence<=1:
                raise ValueError('Invalid decision confidence')
            fallback = fallback or confidence < 0.85
    usage = result.get('usage', {})
    return {'choices':[{'finish_reason':'stop','message':{'role':'assistant','content':json.dumps(value)}}],
            'decision_fallback':fallback,
            'usage':{'prompt_tokens':usage.get('input_tokens',0),'completion_tokens':0,
                     'total_tokens':usage.get('input_tokens',0)}}


class Engine:
    def __init__(self, backend, model):
        os.environ['HF_HUB_OFFLINE']='1'
        os.environ['TRANSFORMERS_OFFLINE']='1'
        os.environ['HF_HUB_DISABLE_TELEMETRY']='1'
        import torch
        torch.set_num_threads(8)
        self.backend=backend
        if backend == 'd1-omni-600m':
            from transformers import AutoModel, AutoTokenizer
            self.model=AutoModel.from_pretrained(model,trust_remote_code=True,local_files_only=True,dtype=torch.float32).eval()
            # Pin the lazy tokenizer to the verified local bundle, too.
            self.model.tokenizer=AutoTokenizer.from_pretrained(model,local_files_only=True)
        elif backend == 'simple-jev-qwen-0.8b':
            sys.path[:0]=[str(Path(model)/'upstream/hf-server'),str(Path(model)/'upstream')]
            from hf_server import load_service
            self.service=load_service(model,device='cpu',dtype='float32',max_model_len=8192,max_batch_size=8,max_batch_tokens=16384)
        else:
            raise ValueError('Unsupported decision backend')

    def decide(self,state,questions):
        if self.backend == 'd1-omni-600m':
            import torch
            with torch.inference_mode():
                return self.model.system_one(state,questions)
        return asyncio.run(self.service.classify({'model':'simple-jev-qwen-0.8b','state':state,'questions':questions}))


def handler_for(engine,secret):
    lock=threading.Lock()
    operations={}
    cancelled=set()
    controls=threading.Lock()
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*args):
            pass
        def send_json(self,status,body):
            encoded=json.dumps(body).encode()
            self.send_response(status);self.send_header('Content-Type','application/json')
            self.send_header('Content-Length',str(len(encoded)));self.end_headers()
            try:self.wfile.write(encoded)
            except (BrokenPipeError,ConnectionResetError):pass
        def authenticated(self):
            if hmac.compare_digest(self.headers.get('Authorization',''),'Bearer '+secret):return True
            self.send_json(401,{'error':'Unauthorized'});return False
        def do_GET(self):
            if not self.authenticated():return
            self.send_json(200 if self.path=='/health' else 404,{'backend':engine.backend,'status':'ok'})
        def do_POST(self):
            if not self.authenticated():return
            if self.path.startswith('/cancel/'):
                identity=self.path[len('/cancel/'):]
                if len(identity)!=32 or any(c not in '0123456789abcdef' for c in identity):
                    self.send_json(400,{'error':'Invalid request id'});return
                with controls:
                    if identity in operations:operations[identity].set()
                    elif len(cancelled)<64:cancelled.add(identity)
                self.send_json(200,{'cancelled':True});return
            if self.path not in ('/v1/chat/completions','/v1/systemone'):
                self.send_json(404,{'error':'Not found'});return
            identity=self.headers.get('X-OpenMic-Request') or uuid.uuid4().hex
            if len(identity)!=32 or any(c not in '0123456789abcdef' for c in identity):
                self.send_json(400,{'error':'Invalid request id'});return
            stopped=threading.Event()
            with controls:
                if len(operations)>=8:
                    self.send_json(429,{'error':'Decision queue full'});return
                if identity in cancelled:stopped.set();cancelled.discard(identity)
                operations[identity]=stopped
            try:
                length=int(self.headers.get('Content-Length','0'))
                if not 0<length<=512*1024:raise ValueError('Request too large')
                payload=json.loads(self.rfile.read(length))
                if self.path=='/v1/chat/completions':state,questions=questions_from_chat(payload)
                else:
                    state,questions=payload['state'],payload['questions']
                    if not isinstance(state,str) or len(state)>50000 or not isinstance(questions,dict) or not 1<=len(questions)<=8:
                        raise ValueError('Invalid decision state')
                with lock:
                    if stopped.is_set():raise InterruptedError()
                    result=engine.decide(state,questions)
                if stopped.is_set():raise InterruptedError()
                self.send_json(200,completion(payload,result) if self.path=='/v1/chat/completions' else result)
            except InterruptedError:self.send_json(499,{'error':'Cancelled'})
            except (ValueError,KeyError,TypeError):self.send_json(422,{'error':'Invalid or unverified decision'})
            except Exception:self.send_json(500,{'error':'Local decision inference failed'})
            finally:
                with controls:operations.pop(identity,None)
    return Handler


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--backend',required=True)
    parser.add_argument('--model',required=True)
    parser.add_argument('--port',type=int,required=True)
    args=parser.parse_args()
    secret=os.environ.pop('OPENMIC_DECISION_TOKEN')
    engine=Engine(args.backend,str(Path(args.model).resolve()))
    ThreadingHTTPServer(('127.0.0.1',args.port),handler_for(engine,secret)).serve_forever()


if __name__=='__main__':main()
