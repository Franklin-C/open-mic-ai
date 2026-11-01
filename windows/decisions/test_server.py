import importlib.util
import json
from pathlib import Path
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer

import server
import setup


def payload():
    return {'messages':[{'role':'system','content':'Verify the edit.'},
                        {'role':'user','content':'Source: Do not include the old invoice. Edit: Include the old invoice.'}],
            'response_format':{'json_schema':{'schema':{'properties':{'safe':{'type':'boolean'}}}}}}


class DecisionChecks(unittest.TestCase):
    def test_boolean_threshold_rejects_uncertainty(self):
        for probability,expected in [(0.1,False),(0.79,False),(0.81,True)]:
            response=server.completion(payload(),{'answers':{'safe':{'noul':probability}}})
            self.assertEqual(json.loads(response['choices'][0]['message']['content'])['safe'],expected)
            self.assertEqual(response['usage']['completion_tokens'],0)

    def test_invalid_probability_cannot_accept_an_edit(self):
        for value in [float('nan'),float('inf'),2,-1]:
            with self.assertRaises(ValueError):server.completion(payload(),{'answers':{'safe':{'noul':value}}})

    def test_chat_conversion_uses_source_as_data(self):
        state,questions=server.questions_from_chat(payload())
        self.assertIn('Do not include',state)
        self.assertEqual(questions['safe']['type'],'noul')
        self.assertIn('Verify the edit',questions['safe']['instructions'])

    def test_choice_must_come_from_catalog(self):
        request=payload();request['response_format']['json_schema']['schema']['properties']={'layout':{'type':'string','enum':['prose','list']}}
        request['decision_criteria']={'list':'Distinct items to buy'}
        self.assertEqual(server.questions_from_chat(request)[1]['layout']['criteria']['list'],'Distinct items to buy')
        with self.assertRaises(ValueError):server.completion(request,{'answers':{'layout':{'choice':'invented'}}})
        uncertain=server.completion(request,{'answers':{'layout':{'choice':'list','confidence':0.7}}})
        self.assertTrue(uncertain['decision_fallback'])

    def test_private_loopback_adapter(self):
        class Fixture:
            backend='fixture'
            def decide(self,state,questions):return {'answers':{'safe':{'noul':0.1}}}
        http=ThreadingHTTPServer(('127.0.0.1',0),server.handler_for(Fixture(),'synthetic-secret'))
        thread=threading.Thread(target=http.serve_forever,daemon=True);thread.start()
        client=urllib.request.build_opener(urllib.request.ProxyHandler({}));url=f'http://127.0.0.1:{http.server_port}'
        try:
            with self.assertRaises(urllib.error.HTTPError) as error:client.open(url+'/health')
            self.assertEqual(error.exception.code,401)
            request=urllib.request.Request(url+'/v1/chat/completions',data=json.dumps(payload()).encode(),headers={'Authorization':'Bearer synthetic-secret'})
            self.assertFalse(json.loads(json.load(client.open(request))['choices'][0]['message']['content'])['safe'])
        finally:http.shutdown();http.server_close();thread.join()

    def test_downloads_require_full_hash_and_size(self):
        with tempfile.TemporaryDirectory() as folder:
            target=Path(folder)/'model';target.write_bytes(b'synthetic')
            item={'bytes':9,'sha256':'wrong'}
            self.assertFalse(setup.verified(target,item))


if __name__=='__main__':unittest.main()
