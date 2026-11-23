"""Offline ElevenLabs asset pipeline. Dry-run is the default; credentials only from env."""
import argparse, array, concurrent.futures, hashlib, json, math, os, re, shutil, subprocess, sys, threading, time, unicodedata, urllib.error, urllib.request
from datetime import datetime, timezone
from pathlib import Path

RATE = 40  # Official Sound Effects overview, verified 2026-11-04; see API_Notas.md.
MIN_SECONDS, MAX_SECONDS = .5, 30  # Stricter API reference takes precedence over overview's .1.
def now(): return datetime.now(timezone.utc).isoformat()
def norm(s): return ''.join(c for c in unicodedata.normalize('NFD',s.casefold()) if not unicodedata.combining(c))
def atomic(path, value):
    path.parent.mkdir(parents=True,exist_ok=True); tmp=path.with_suffix(path.suffix+'.tmp');tmp.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8');tmp.replace(path)
def variants(section,name,loop,seconds):
    if loop:return 1
    if name in ('sting_mirror','glass_shatter_mirror'):return 2
    s=norm(section)
    return 3 if s.startswith('jogador') or ('portas' in s and 'objetos' in s) or ('ratos' in s and 'predio' in s) or ('terror' in s and seconds<=5) else 1
def parse(text):
    items=[]; invalid=[]; seen=set();section=None;influence=None
    for number,line in enumerate(text.splitlines(),1):
        if not line.strip() or line.lstrip().startswith('#') and not line.lstrip().startswith('##'):continue
        if line.lstrip().startswith('##') or re.search(r'\(influence\b',line,re.I):
            match=re.fullmatch(r'\s*(?:##\s+)?(.+?)\s*\(influence\s+([+-]?[0-9]+(?:[.,][0-9]+)?)\)\s*',line,re.I)
            section=influence=None
            if match:
                section=match[1].strip();influence=float(match[2].replace(',','.'))
                if not 0<=influence<=1:section=influence=None
            if section is None:invalid.append({'line':number,'reason':'Seção/influence inválido','text':line})
            continue
        try:
            if section is None:raise ValueError('Linha sem seção com influence válido')
            cells=line.split('|',3)
            if len(cells)!=4:raise ValueError('Esperados arquivo | segundos | loop | prompt')
            filename,raw_seconds,raw_loop,prompt=(s.strip() for s in cells)
            name=Path(filename).stem if Path(filename).suffix in ('.wav','.mp3','.ogg') else filename
            if not name or name!=name.lower() or not all(c.islower() or c.isdigit() or c in '_-' for c in name):raise ValueError('Nome precisa ser minúsculo, sem espaço/caminho; usar letras, números, _ ou -')
            if name in seen:raise ValueError('Nome duplicado')
            seconds=float(raw_seconds.replace(',','.'))
            if not math.isfinite(seconds) or not .1<=seconds<=MAX_SECONDS:raise ValueError('Duração fora de0,1–30s')
            if norm(raw_loop) not in ('sim','nao'):raise ValueError('Loop precisa ser sim/não')
            if not prompt:raise ValueError('Prompt vazio')
            loop=norm(raw_loop)=='sim';count=variants(section,name,loop,seconds);seen.add(name)
            if loop and seconds<MIN_SECONDS:raise ValueError('Loop precisa ter pelo menos0,5s')
            items.append({'nome':name,'secao':section,'prompt':prompt,'original_line':line,'duracao':seconds,'api_duracao':max(MIN_SECONDS,seconds),'ajuste':('API recebe0,5s; recorte local após remover silêncio, sem mudar prompt' if seconds<MIN_SECONDS else ''),'loop':loop,'influence':influence,'variantes':[{'nome':name+(f'_{i+1:02}' if count>1 else ''),'estado':'pendente'} for i in range(count)]})
        except ValueError as exc:invalid.append({'line':number,'reason':str(exc),'text':line})
    return items,invalid

class Halt(Exception):pass
class AssetRejected(Halt):pass
def safe_error(error):
    value=str(error);key=os.getenv('ELEVENLABS_API_KEY')
    if key:value=value.replace(key,'[REDACTED]')
    return re.sub(r'\bsk[_-][\w-]{16,}\b','[REDACTED]',value)
def api_request(method,path,payload=None):
    key=os.getenv('ELEVENLABS_API_KEY')
    if not key:raise Halt('ELEVENLABS_API_KEY não está no ambiente; nenhuma geração iniciada')
    request=urllib.request.Request('https://api.elevenlabs.io'+path,data=json.dumps(payload).encode() if payload is not None else None,headers={'xi-api-key':key,'Content-Type':'application/json'},method=method)
    return urllib.request.urlopen(request,timeout=180)
def balance():
    with api_request('GET','/v1/user/subscription') as r:s=json.load(r)
    if not isinstance(s.get('character_count'),(int,float)) or not isinstance(s.get('character_limit'),(int,float)):raise Halt('Saldo em créditos não verificável; parar antes de gerar')
    return {'plan':s.get('tier','verificar'),'used':s['character_count'],'limit':s['character_limit'],'available':s['character_limit']-s['character_count'],'date':now()}
def category(item,config):
    n=norm(item['nome'])
    for rule in config['rules']:
        if any(n.startswith(prefix) for prefix in rule['prefixes']):return dict(rule)
    return dict(config['default'])

def is_long(item,config):
    return item['loop'] or item['categoria']=='Ambience' or item.get('lufs') is not None or item['duracao']>config.get('long_seconds',8)
def run_ffmpeg(binary,args):
    p=subprocess.run([binary,'-hide_banner','-nostdin','-y',*args],capture_output=True)
    if p.returncode:raise Halt('ffmpeg: '+p.stderr.decode('utf8','replace')[-1800:])
    return p.stdout,p.stderr.decode('utf8','replace')
def samples(binary,path,channels):
    raw,_=run_ffmpeg(binary,['-i',str(path),'-ar','48000','-ac',str(channels),'-f','f32le','pipe:1']);a=array.array('f');a.frombytes(raw)
    if sys.byteorder!='little':a.byteswap()
    if not a or any(not math.isfinite(x) for x in a):raise AssetRejected('Áudio vazio/não finito')
    rms=math.sqrt(sum(x*x for x in a)/len(a));return a,{'seconds':len(a)/(48000*channels),'rms':rms,'peak':max(abs(x) for x in a),'channels':channels,'sample_rate':48000}
def seam(a,channels):
    width=min(1024,len(a)//(channels*4));jump=max(abs(a[-channels+c]-a[c]) for c in range(channels));front=a[:width*channels];tail=a[-width*channels:];power1=sum(x*x for x in front);power2=sum(x*x for x in tail);corr=sum(x*y for x,y in zip(front,tail))/max(1e-15,math.sqrt(power1*power2));local=math.sqrt((power1+power2)/max(1,2*width*channels));ratio=math.sqrt(max(power1,power2)/max(1e-15,min(power1,power2)))
    return {'amplitude_jump':jump,'phase_correlation':corr,'rms_ratio':ratio,'suspect':jump>max(.02,local*3) or ratio>3}

def process_audio(binary,source,dest,item,config,cache):
    channels=1 if item['spatial'] else config.get('stereo_channels',2);original,rawqa=samples(binary,source,channels)
    if rawqa['rms']<config['minimum_rms']:raise AssetRejected('Bruto vazio/silencioso (RMS abaixo do piso)')
    requested=item.get('api_duracao',item['duracao'])
    if abs(rawqa['seconds']-requested)>max(.3,requested*.03):raise AssetRejected('Duração do bruto diferente da solicitada')
    cache.mkdir(parents=True,exist_ok=True);temp=cache/(dest.stem+'.working.wav');filters=[]
    if not item['loop']:
        # Only trim long edge silence; a transient need not sustain for60ms to survive.
        threshold=min(10**(config['silence_db']/20),rawqa['peak']*.02)
        active=[n//channels for n,x in enumerate(original) if abs(x)>threshold]
        if not active:raise AssetRejected('Nenhuma amostra acima do limiar de silêncio')
        first,last=active[0]/48000,active[-1]/48000
        start=max(0,first-.01) if first>.06 else 0
        end=min(rawqa['seconds'],last+.01) if rawqa['seconds']-last>.06 else rawqa['seconds']
        filters=[f'atrim=start={start}:end={end}','asetpts=PTS-STARTPTS']
        if requested>item['duracao']:filters.append('atrim=duration='+str(item['duracao']))
    run_ffmpeg(binary,['-i',str(source),'-ar','48000','-ac',str(channels),*(['-af',','.join(filters)] if filters else []),'-c:a','pcm_s16le',str(temp)])
    _,qa=samples(binary,temp,channels);long=is_long(item,config)
    if long:
        target=item.get('lufs',config['ambience_lufs']);target=config['ambience_lufs'] if target is None else target
        _,stderr=run_ffmpeg(binary,['-i',str(temp),'-af',f'loudnorm=I={target}:TP=-3:LRA=7:print_format=json','-f','null','-']);match=re.search(r'\{\s*"input_i".*?\}',stderr,re.S)
        if not match:raise Halt('Medição LUFS indisponível')
        measured=json.loads(match[0]);keys=['input_i','input_tp','input_lra','input_thresh','target_offset']
        if not all(math.isfinite(float(measured[k])) for k in keys):raise Halt('Medição LUFS não finita')
        effect=f"loudnorm=I={target}:TP=-3:LRA=7:measured_I={measured['input_i']}:measured_TP={measured['input_tp']}:measured_LRA={measured['input_lra']}:measured_thresh={measured['input_thresh']}:offset={measured['target_offset']}:linear=true"
    else:
        gain=config['short_peak_db']-20*math.log10(max(qa['peak'],1e-15));fade=config['fade_seconds'];effect=f'volume={gain}dB,afade=t=in:d={fade},afade=t=out:st={max(0,qa["seconds"]-fade)}:d={fade}'
    dest.parent.mkdir(parents=True,exist_ok=True);run_ffmpeg(binary,['-i',str(temp),'-af',effect,'-ar','48000','-ac',str(channels),*(['-c:a','libvorbis','-q:a','6'] if long else ['-c:a','pcm_s16le']),str(dest)])
    audio,final=samples(binary,dest,channels);final.update({'raw':rawqa,'target_peak_db':None if long else config['short_peak_db'],'target_lufs':target if long else None,'requested_seconds':item['duracao'],'api_seconds':requested})
    if item['loop']:
        final['seam_before']=seam(audio,channels)
        if final['seam_before']['suspect']:
            # Circular tail/head crossfade; loop becomes 50ms shorter, not trimmed for silence.
            duration=final['seconds'];cross=min(.05,duration/8);fixed=cache/(dest.stem+'.fixed.ogg')
            graph=f'[0:a]asplit=3[b][t][h];[b]atrim=start={cross}:end={duration-cross},asetpts=PTS-STARTPTS[body];[t]atrim=start={duration-cross},asetpts=PTS-STARTPTS[tail];[h]atrim=end={cross},asetpts=PTS-STARTPTS[head];[tail][head]acrossfade=d={cross}:c1=tri:c2=tri[blend];[body][blend]concat=n=2:v=0:a=1[out]'
            run_ffmpeg(binary,['-i',str(dest),'-filter_complex',graph,'-map','[out]','-ar','48000','-ac',str(channels),'-c:a','libvorbis','-q:a','6',str(fixed)]);fixed.replace(dest);audio,after=samples(binary,dest,channels);final.update(after);final['crossfade_seconds']=cross
        final['seam']=seam(audio,channels)
    if final['rms']<config['minimum_rms'] or final['peak']>1.001:raise AssetRejected('Processado silencioso/clipping')
    temp.unlink(missing_ok=True);return final

class Pipeline:
    def __init__(self,args):
        self.a=args;self.root=Path(args.project).resolve();self.docs=self.root/'Assets/Docs/Audio';self.path=self.docs/'sfx_manifest.json';self.config=json.loads((self.root/'Tools/Audio/sfx_config.json').read_text(encoding='utf-8-sig'));self.lock=threading.RLock();self.stop=threading.Event();self.jobs=[]
        try:self.old=json.loads(self.path.read_text(encoding='utf-8-sig'))
        except FileNotFoundError:self.old={}
        source=Path(args.list) if args.list else self.docs/'sfx_lista.txt';self.exists=source.exists();items,invalid=parse(source.read_text(encoding='utf-8-sig')) if self.exists else ([],[])
        old={x['nome']:x for x in self.old.get('items',[])}
        for item in items:
            rule=category(item,self.config);item.update({k:rule[k] for k in ['categoria','mixer','spatial','volume','pitchMin','pitchMax','lufs']});prev=old.get(item['nome']);same=prev and self.old.get('model')==args.model and all(prev.get(k)==item.get(k) for k in ('prompt','duracao','loop','influence'))
            if same:
                previous={v['nome']:v for v in prev.get('variantes',[])};item['variantes']=[previous.get(v['nome'],v) for v in item['variantes']]
            selected=(not args.only or item['nome']==args.only or any(v['nome']==args.only for v in item['variantes'])) and (not args.section or norm(item['secao'])==norm(args.section))
            if selected:
                for v in item['variantes']:
                    if args.only and args.only!=item['nome'] and args.only!=v['nome']:continue
                    force=bool(args.only or args.section);valid=v.get('estado')=='gerado' and self.valid_file(v.get('processed','')) and (not v.get('sha256') or hashlib.sha256((self.root/v['processed']).read_bytes()).hexdigest()==v['sha256'])
                    if force or not valid and not v.get('requires_review'):self.jobs.append((item,v))
        self.manifest={'version':1,'date':now(),'input_missing':not self.exists,'items':items,'invalid_lines':invalid,'ledger':self.old.get('ledger',[]),'credit_baseline':self.old.get('credit_baseline'),'balance':self.old.get('balance'),'model':args.model,'pricing':{'date':'2026-11-04','credits_per_second':RATE,'safety_per_generation_floor':200,'note':'Conservative max(40*seconds,200); overview40/s, pricing FAQ200/generation, API page alsoUSD. Actual character-cost header takes precedence. No USD/credit conversion invented.','source':'https://elevenlabs.io/docs/overview/capabilities/sound-effects'},'test_passed':self.old.get('test_passed',False) and self.old.get('model')==args.model}
        if args.only and not any(x['nome']==args.only or any(v['nome']==args.only for v in x['variantes']) for x in items):self.manifest['selection_error']='--only não encontrado'
        if args.section and not any(norm(x['secao'])==norm(args.section) for x in items):self.manifest['selection_error']='--section não encontrada'
        self.save()
    def valid_file(self,p):
        path=(self.root/p).resolve();return bool(p) and path.is_relative_to(self.root/'Assets/Audio') and path.is_file() and path.stat().st_size>0
    def save(self):
        with self.lock:
            for i in self.manifest['items']:
                states=[v['estado'] for v in i['variantes']];i['estado']='gerado' if all(s=='gerado' for s in states) else 'falhou' if 'falhou' in states else 'pendente';i['tamanho_arquivo']=sum(v.get('bytes',0) for v in i['variantes'])
            atomic(self.path,self.manifest)
    @staticmethod
    def estimate(item):return max(200,math.ceil(item.get('api_duracao',item['duracao'])*RATE))
    def report(self,blocked=None):
        summary={};total=seconds=0
        for x in self.manifest['items']:
            row=summary.setdefault(x['secao'],{'sons':0,'variantes':0,'seconds':0});n=len(x['variantes']);row['sons']+=1;row['variantes']+=n;row['seconds']+=n*x['duracao'];total+=n;seconds+=n*x['duracao']
        generated=sum(v['estado']=='gerado' for i in self.manifest['items'] for v in i['variantes'])
        failed=sum(v['estado']=='falhou' for i in self.manifest['items'] for v in i['variantes'])
        value={'date':now(),'dry_run':not self.a.run,'sections':summary,'sounds':len(self.manifest['items']),'variants':total,'seconds':seconds,'generated_variants':generated,'failed_variants':failed,'remaining_variants':total-generated,'pending_requests':len(self.jobs),'pending_estimate_credits':sum(self.estimate(i) for i,v in self.jobs),'max_credits':self.a.max_credits,'stop_at':self.a.max_credits*.9,'invalid_lines':self.manifest['invalid_lines'],'input_missing':not self.exists,'blocked':blocked,'balance':self.manifest.get('balance'),'spent_actual':sum(x.get('actual') or 0 for x in self.manifest['ledger']),'spent_conservative':sum(x['reserved'] for x in self.manifest['ledger'])};atomic(self.docs/'sfx_dry_run.json',value);print(json.dumps(value,ensure_ascii=False));return value
    def reserve(self,item):
        with self.lock:
            cost=self.estimate(item);spent=sum(x['reserved'] for x in self.manifest['ledger'])
            if self.stop.is_set() or spent+cost>=self.a.max_credits*.9:self.stop.set();raise Halt('Parada de segurança:90%do teto (inclui reservas em voo)')
            base=self.manifest.get('credit_baseline')
            if base and cost>base['limit']-base['used']-(spent-base['ledger_before']):
                self.stop.set();raise Halt('Parada de segurança: saldo não cobre a reserva, incluindo tentativas anteriores/em voo')
            ticket={'date':now(),'nome':item['nome'],'reserved':cost,'actual':None,'status':'reserved'};self.manifest['ledger'].append(ticket);self.save();return ticket
    def generate(self,item,v):
        key=os.environ.get('ELEVENLABS_API_KEY');assert key
        tier=self.manifest['balance']['plan'];fmt='mp3_44100_192' if tier in ('creator','pro','scale','business','enterprise') else 'mp3_44100_128'
        if not item['loop'] and tier in ('pro','scale','business','enterprise'):fmt='wav_48000'
        suffix='.wav' if fmt.startswith('wav') else '.mp3';raw=self.root/'Assets/Audio/Raw/Editor'/(v['nome']+suffix)
        reusable=not (self.a.only or self.a.section) and self.valid_file(v.get('raw','')) and v.get('model')==self.a.model and v.get('raw_sha256')==hashlib.sha256((self.root/v['raw']).read_bytes()).hexdigest()
        request_id=v.get('request_id','')
        if reusable:raw=self.root/v['raw'];fmt=v.get('format',fmt)
        for attempt in ([] if reusable else range(4)):
            ticket=self.reserve(item)
            try:
                with api_request('POST','/v1/sound-generation?output_format='+fmt,{'text':item['prompt'],'duration_seconds':item.get('api_duracao',item['duracao']),'loop':item['loop'],'prompt_influence':item['influence'],'model_id':self.a.model}) as response:
                    audio=response.read();billed=response.headers.get('character-cost');request_id=response.headers.get('request-id','')
                with self.lock:
                    if billed is not None and re.fullmatch(r'\d+(?:\.\d+)?',billed):ticket['actual']=float(billed);ticket['reserved']=float(billed)
                    ticket['status']='received';self.save()
                if len(audio)<128:raise Halt('Resposta binária vazia')
                raw.parent.mkdir(parents=True,exist_ok=True)
                if raw.exists():self.backup(raw)
                raw.write_bytes(audio)
                with self.lock:
                    v.update({'raw':raw.relative_to(self.root).as_posix(),'raw_sha256':hashlib.sha256(audio).hexdigest(),'model':self.a.model,'format':fmt,'request_id':request_id});self.save()
                break
            except urllib.error.HTTPError as error:
                ticket['status']='http_'+str(error.code);self.save()
                try:code=json.loads(error.read()).get('detail',{}).get('status','')
                except (ValueError,AttributeError):code=''
                if code in ('quota_exceeded','insufficient_credits','invalid_api_key','missing_permissions'):self.stop.set();raise Halt('Parada de segurança: '+code) from None
                if error.code in (401,403,402):self.stop.set();raise Halt('Autenticação/permissão/saldo: HTTP'+str(error.code)) from None
                if error.code==429 or 500<=error.code<600:
                    if attempt==3:raise Halt('Limite/servidor após4tentativas') from None
                    try:retry=float(error.headers.get('retry-after','0') or 0)
                    except ValueError:retry=0
                    time.sleep(min(20,2**attempt+retry));continue
                raise Halt('API recusou a linha: HTTP'+str(error.code)+'; prompt preservado') from None
            except (urllib.error.URLError,TimeoutError):
                ticket['status']='network_uncertain';self.stop.set();self.save();raise Halt('Falha de rede; custo incerto reservado, sem repetição automática') from None
        long=is_long(item,self.config)
        dest=self.root/('Assets/Audio/Ambience' if item['categoria']=='Ambience' else 'Assets/Audio/SFX/'+item['categoria'])/(v['nome']+('.ogg' if long else '.wav'))
        if not dest.resolve().is_relative_to(self.root/'Assets/Audio'):raise Halt('Destino de categoria fora de Assets/Audio')
        staging=self.root/'Tools/Audio/.cache_sfx/output'/dest.name
        qa=process_audio(self.ffmpeg,raw,staging,item,self.config,self.root/'Tools/Audio/.cache_sfx')
        if item['loop'] and qa.get('seam',{}).get('suspect'):raise AssetRejected('Loop ainda tem emenda suspeita após crossfade; processado anterior preservado')
        if dest.exists():self.backup(dest)
        dest.parent.mkdir(parents=True,exist_ok=True);staging.replace(dest)
        with self.lock:
            v.update({'estado':'gerado','requires_review':False,'erro':None,'data':now(),'bytes':dest.stat().st_size,'raw':raw.relative_to(self.root).as_posix(),'processed':dest.relative_to(self.root).as_posix(),'duracao':qa['seconds'],'model':self.a.model,'format':fmt,'request_id':request_id,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'qa':qa});self.save()
        print(json.dumps({'gerado':v['nome'],'seconds':qa['seconds'],'loop_suspect':qa.get('seam',{}).get('suspect',False)}))
    def backup(self,p):
        target=self.root/'Assets/_Substituidos/SfxBatch20261104'/p.relative_to(self.root);target=target.with_name(target.name+'.'+str(time.time_ns())+'.bak');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
    def one(self,job):
        i,v=job
        try:self.generate(i,v)
        except Exception as e:
            with self.lock:v.update({'estado':'falhou','erro':safe_error(e),'requires_review':isinstance(e,AssetRejected),'data':now()});self.save()
            raise
    def initial_tests(self):
        short=next((j for j in self.jobs if not j[0]['loop'] and j[0]['duracao']<=3),None)
        loop=next((j for j in self.jobs if j[0]['loop'] and j[0]['duracao']>=10),None)
        ambient=next((j for j in self.jobs if j!=loop and j[0]['categoria']=='Ambience'),None)
        if not all((short,loop,ambient)):raise Halt('Teste inicial exige curto, loop>=10s e outro ambiente; rode sem filtro primeiro')
        return [short,loop,ambient]
    def safe_balance(self,observed,spent):
        # Subscription counters can lag behind successful, billed responses.
        base=self.manifest.get('credit_baseline')
        if not base:
            base={'used':observed.get('used',0),'limit':observed.get('limit',observed['available']),'ledger_before':spent,'date':now()};self.manifest['credit_baseline']=base
        conservative=max(0,base['limit']-base['used']-(spent-base['ledger_before']))
        observed['effective_available']=min(observed['available'],conservative)
        return observed['effective_available']
    def fit_budget(self,tests,available):
        # Whole items keep their variants together; the first three QA jobs always go first.
        selected=list(tests);cost=sum(self.estimate(i) for i,v in selected)
        priority=self.config.get('priority',[]);order={name:n for n,name in enumerate(priority)}
        items=sorted(self.manifest['items'],key=lambda i:order.get(i['nome'],len(order)+self.manifest['items'].index(i)))
        for item in items:
            group=[j for j in self.jobs if j[0] is item and j not in selected];extra=sum(self.estimate(i) for i,v in group)
            if cost+extra<=available:selected.extend(group);cost+=extra
        self.manifest['batch_selection']={'date':now(),'budget':available,'estimate':cost,'variants':[v['nome'] for i,v in selected]};self.save()
        return selected
    def run(self):
        if not self.exists or not self.manifest['items']:return self.report('Falta lista válida; nenhum pedido à API')
        if self.manifest.get('selection_error'):return self.report(self.manifest['selection_error'])
        self.report()
        if not self.a.run:return
        try:
            self.ffmpeg=os.environ.get('FFMPEG_BINARY') or shutil.which('ffmpeg') or self.config.get('ffmpeg')
            if not self.ffmpeg or not Path(self.ffmpeg).is_file():raise Halt('ffmpeg ausente; ver COMO_GERAR.md')
            self.manifest['balance']=balance();spent=sum(x['reserved'] for x in self.manifest['ledger']);available=self.safe_balance(self.manifest['balance'],spent);self.save()
            tests=self.initial_tests() if not self.manifest['test_passed'] else []
            if self.a.test:self.jobs=list(tests)
            elif self.a.budget_fit:self.jobs=self.fit_budget(tests,min(available*.9,self.a.max_credits*.9-spent-1))
            if self.a.budget_fit and not self.jobs and any(v['estado']!='gerado' for i in self.manifest['items'] for v in i['variantes']):
                return self.report('Parada de segurança: saldo com margem não cobre o próximo item completo; pendências preservadas')
            need=sum(self.estimate(i) for i,v in self.jobs)
            if need>available:raise Halt('Saldo não cobre estimativa; nenhuma geração iniciada')
            if spent+need>=self.a.max_credits*.9:raise Halt('Estimativa alcança90%do teto; nenhuma geração iniciada')
            if not self.manifest['test_passed']:
                for job in tests:
                    self.one(job)
                    if job[1].get('qa',{}).get('seam',{}).get('suspect'):raise Halt('Loop de teste com emenda suspeita; revisar antes dos lotes')
                self.manifest['test_passed']=True;self.save();self.jobs=[j for j in self.jobs if j not in tests]
            if self.a.test:return self.report('Três testes concluídos; lote restante não iniciado por--test')
            workers=min(self.a.concurrency,2 if self.manifest['balance']['plan']=='free' else 3)
            with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
                for start in range(0,len(self.jobs),workers):
                    futures=[pool.submit(self.one,j) for j in self.jobs[start:start+workers]]
                    for f in futures:
                        try:f.result()
                        except AssetRejected as error:print(json.dumps({'rejeitado':safe_error(error),'acao':'Bruto preservado para revisão; sem repetição paga automática'}))
                    time.sleep(.3)
            self.jobs=[];self.report('Lote selecionado concluído; consultar variantes pendentes no manifesto')
        except Exception as e:self.stop.set();self.report(safe_error(e))

def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',default=str(Path(__file__).resolve().parents[2]));p.add_argument('--list');p.add_argument('--run',action='store_true');p.add_argument('--dry-run',action='store_true');p.add_argument('--test',action='store_true');p.add_argument('--budget-fit',action='store_true',help='Generate a resumable priority subset within90%%of verified balance');p.add_argument('--only');p.add_argument('--section');p.add_argument('--max-credits',type=int,default=int(os.environ.get('MAX_CREDITS','30000')));p.add_argument('--concurrency',type=int,choices=[2,3],default=2);p.add_argument('--model',choices=['eleven_text_to_sound_v2','eleven_text_to_sound_v3'],default='eleven_text_to_sound_v3');args=p.parse_args(argv)
    if args.dry_run:args.run=False
    if args.max_credits<=0:p.error('max-credits precisa ser positivo')
    root=Path(args.project).resolve();lock=root/'Tools/Audio/.sfx.lock';lock.parent.mkdir(parents=True,exist_ok=True)
    try:fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY)
    except FileExistsError:raise SystemExit('Outra execução/lock ativo: '+str(lock)+'; confira o processo antes de remover um lock antigo')
    try:
        os.write(fd,str(os.getpid()).encode());os.close(fd);pipeline=Pipeline(args);pipeline.run()
        # Budget sub-batches defer repeated full imports until no affordable jobs remain.
        defer_import=args.budget_fit and bool(pipeline.manifest.get('batch_selection',{}).get('variants')) and any(v.get('estado')=='pendente' for i in pipeline.manifest['items'] for v in i['variantes'])
        if args.run and not defer_import and any(v.get('estado')=='gerado' for i in pipeline.manifest['items'] for v in i['variantes']):
            cli=shutil.which('unity')
            if cli:
                try:
                    result=subprocess.run([cli,'command','menu','--project-path',str(root),'--timeout','120','--caller','plugin','--skill','unity-cli','--format','json','--','--path','Tools/Áudio/Importar SFX'],capture_output=True,text=True,encoding='utf8',errors='replace',timeout=150)
                    atomic(pipeline.docs/'sfx_auto_import.json',{'date':now(),'exit_code':result.returncode,'response':json.loads(result.stdout)})
                    print('Importação: consultar sfx_auto_import.json; se adiada, usar Tools > Áudio > Importar SFX')
                except (OSError,subprocess.TimeoutExpired,ValueError):print('Importação automática indisponível; usar Tools > Áudio > Importar SFX quando o Editor estiver pronto')
    finally:lock.unlink(missing_ok=True)
if __name__=='__main__':main()
