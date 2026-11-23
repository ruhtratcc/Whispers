"""Offline tests; mocks explicitly forbid a paid or authenticated request."""
import argparse, contextlib, io, json, os, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
import sfx_pipeline as s

class PipelineTests(unittest.TestCase):
    def args(self, root, **kw):
        return argparse.Namespace(project=str(root),list=None,run=False,dry_run=True,test=False,
            only=None,section=None,max_credits=30000,concurrency=2,model='eleven_text_to_sound_v3',budget_fit=False,**kw)
    def fixture(self,root):
        (root/'Tools/Audio').mkdir(parents=True)
        (root/'Tools/Audio/sfx_config.json').write_bytes(Path(__file__).with_name('sfx_config.json').read_bytes())
        docs=root/'Assets/Docs/Audio';docs.mkdir(parents=True)
        (docs/'sfx_lista.txt').write_text('Ambiências (influence 0,4)\namb_one | 30 | sim | exact ambient\namb_two | 30 | sim | exact second\n## Jogador (influence 0.7)\nfoot_tile_walk | 0.5 | não | exact step\n','utf8')
    def test_full_input_and_influence(self):
        items,invalid=s.parse((Path(__file__).parents[2]/'Assets/Docs/Audio/sfx_lista.txt').read_text('utf-8-sig'))
        self.assertFalse(invalid);self.assertEqual(162,len(items))
        self.assertEqual(314,sum(len(i['variantes']) for i in items))
        self.assertTrue(all(i['influence']==.4 for i in items if i['secao']=='Ambiências'))
    def test_invalids_continue_and_prompt_is_exact(self):
        items,invalid=s.parse('## Teste (influence 0,7)\ngood | 1 | não | hello  | literal\nBad | 1 | sim | p\n../bad | 1 | sim | p\ngood | 1 | sim | p\nzero | 0 | sim | p\nnext | 1 | não | ok\n')
        self.assertEqual(4,len(invalid));self.assertEqual('hello  | literal',items[0]['prompt']);self.assertEqual('next',items[-1]['nome'])
    def test_short_ui_preserves_request(self):
        items,invalid=s.parse('Interface (influence 0,7)\nui_click | 0.2 | não | Dry click')
        self.assertFalse(invalid);self.assertEqual(.2,items[0]['duracao']);self.assertEqual(.5,items[0]['api_duracao']);self.assertEqual('Dry click',items[0]['prompt'])
    def test_variant_rules(self):
        for section in ['Jogador','Portas, móveis e objetos','Ratos e sons do prédio','Terror']:
            self.assertEqual(3,s.variants(section,'short',False,1));self.assertEqual(1,s.variants(section,'short',True,20))
        self.assertEqual(2,s.variants('Terror','sting_mirror',False,2));self.assertEqual(2,s.variants('Terror','glass_shatter_mirror',False,3));self.assertEqual(1,s.variants('PC retrô','pc_boot',False,5))
    def test_dry_run_does_not_touch_network(self):
        with tempfile.TemporaryDirectory() as d,patch.object(s,'api_request',side_effect=AssertionError('NETWORK FORBIDDEN')):
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root))
            with contextlib.redirect_stdout(io.StringIO()):p.run()
            self.assertEqual(5,len(p.jobs));self.assertFalse(p.manifest['ledger'])
    def test_resume_checks_hash_and_only_forces(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root));i,v=p.jobs[0]
            f=root/'Assets/Audio/SFX/test.wav';f.parent.mkdir(parents=True);f.write_bytes(b'fixture')
            v.update(estado='gerado',processed=f.relative_to(root).as_posix(),sha256=s.hashlib.sha256(f.read_bytes()).hexdigest());p.save()
            self.assertEqual(4,len(s.Pipeline(self.args(root)).jobs))
            args=self.args(root);args.only=i['nome'];self.assertEqual(1,len(s.Pipeline(args).jobs))
            args.only='foot_tile_walk_02';self.assertEqual(1,len(s.Pipeline(args).jobs));self.assertEqual('foot_tile_walk_02',s.Pipeline(args).jobs[0][1]['nome'])
            f.write_bytes(b'corrupt');self.assertEqual(5,len(s.Pipeline(self.args(root)).jobs))
    def test_no_secret_in_errors(self):
        with patch.dict(os.environ,{'ELEVENLABS_API_KEY':'secret-test-value'}):
            self.assertNotIn('secret-test-value',s.safe_error('bad secret-test-value'))
        self.assertNotIn('sk_12345678901234567890',s.safe_error('bad sk_12345678901234567890'))
    def test_budget_reserves_in_flight(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root));p.a.max_credits=300
            p.reserve({'nome':'a','duracao':1})
            with self.assertRaises(s.Halt):p.reserve({'nome':'b','duracao':1})
            self.assertEqual(1,len(p.manifest['ledger']))
    def test_fit_budget_keeps_tests_and_whole_variants(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root));tests=p.initial_tests();jobs=p.fit_budget(tests,3000)
            self.assertEqual(tests,jobs[:3]);self.assertLessEqual(sum(p.estimate(i) for i,v in jobs),3000)
    def test_stale_provider_balance_cannot_restore_spent_credits(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root));p.manifest['credit_baseline']={'used':0,'limit':10000,'ledger_before':0}
            self.assertEqual(433,p.safe_balance({'used':8773,'available':1227,'limit':10000},9567))
    def test_retry_reservations_also_respect_account_budget(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root));p.manifest['credit_baseline']={'used':0,'limit':300,'ledger_before':0}
            p.reserve({'nome':'retry','duracao':1})
            with self.assertRaises(s.Halt):p.reserve({'nome':'retry','duracao':1})
            self.assertEqual(1,len(p.manifest['ledger']))
    def test_unaffordable_budget_subset_stops_without_paid_calls(self):
        with tempfile.TemporaryDirectory() as d,patch.object(s,'balance',return_value={'plan':'free','available':100,'limit':100,'used':0}),patch.object(s,'api_request',side_effect=AssertionError('NETWORK FORBIDDEN')):
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root));p.a.run=True;p.a.budget_fit=True;p.manifest['test_passed']=True
            with patch.dict(os.environ,{'FFMPEG_BINARY':__file__}),contextlib.redirect_stdout(io.StringIO()):result=p.run()
            self.assertIn('Parada de segurança',result['blocked']);self.assertFalse(p.manifest['ledger'])
    def test_balance_too_low_never_posts(self):
        with tempfile.TemporaryDirectory() as d,patch.object(s,'balance',return_value={'plan':'free','available':1}),patch.object(s,'api_request',side_effect=AssertionError('NETWORK FORBIDDEN')):
            root=Path(d);self.fixture(root);p=s.Pipeline(self.args(root));p.a.run=True
            with patch.dict(os.environ,{'FFMPEG_BINARY':__file__}),contextlib.redirect_stdout(io.StringIO()):p.run()
            report=json.loads((p.docs/'sfx_dry_run.json').read_text('utf8'));self.assertIn('Saldo',report['blocked']);self.assertFalse(p.manifest['ledger'])
    def test_categories_follow_actual_names(self):
        c=json.loads(Path(__file__).with_name('sfx_config.json').read_text('utf8'))
        for name,cat in [('amb_drone_mirror','Ambience'),('foot_tile_walk','Player'),('mouse_click','PC'),('hud_swap','UI'),('phone_notify','Phone')]:
            r=s.category({'nome':name},c);self.assertEqual(cat,r['categoria'])
        self.assertEqual(-32,s.category({'nome':'amb_drone_mirror'},c)['lufs'])

if __name__=='__main__':unittest.main()
