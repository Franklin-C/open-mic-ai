"""Live, synthetic checks for installed dedicated decision backends."""
import argparse
import importlib.util
import json
from pathlib import Path
import time


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1])
    parser.add_argument('backend',choices=['d1-omni-600m','simple-jev-qwen-0.8b'])
    args=parser.parse_args();root=args.root.resolve()
    spec=importlib.util.spec_from_file_location('decision_adapter',root/'decisions/server.py')
    adapter=importlib.util.module_from_spec(spec);spec.loader.exec_module(adapter)
    start=time.perf_counter();engine=adapter.Engine(args.backend,str(root/'models/decisions'/args.backend))
    print('Loaded '+args.backend+f' in {time.perf_counter()-start:.2f}s',flush=True)
    questions={
        'layout':{'type':'choice','instructions':'Choose the output format that fits this message.',
                  'criteria':{'email':'An email to a named recipient with greeting and sign-off.',
                              'list':'A shopping list of items to buy.',
                              'prose':'An ordinary short message or question.'}},
        'contains_bananas':{'type':'noul','instructions':'Does the user ask to buy bananas?'}}
    cases=[]
    for state,expected,banana in [('Buy two apples and three bananas.','list',True),
                                  ('Dear Maya, please send the blue folder before Friday. Thanks, Lee.','email',False),
                                  ('Could you send the blue folder before Friday?','prose',False)]:
        began=time.perf_counter();result=engine.decide(state,questions)
        answer=result['answers'];probability=answer['contains_bananas']['noul']
        correct=answer['layout']['choice']==expected
        passed=(correct or answer['layout']['confidence']<0.85) and (probability>=0.5)==banana and result['usage']['output_tokens']==0
        row={'state':state,'expected':expected,'correct':correct,'result':result,'seconds':time.perf_counter()-began,'pass':passed};cases.append(row)
        print(json.dumps(row),flush=True)
    output=root/'work'/('decision-'+args.backend+'.json');output.write_text(json.dumps({'complete':all(row['pass'] for row in cases),'cases':cases},indent=2),encoding='utf-8')
    return 0 if all(row['pass'] for row in cases) else 1


if __name__=='__main__':raise SystemExit(main())
