"""Prepare the licensed downloads in Temp/AudioImport (numpy + soundfile).

Source recordings stay outside Assets. Generated clips, provenance and the
Unity configuration manifest are deterministic; no game settings are edited.
"""
from pathlib import Path
import hashlib
import json
import sys
import subprocess

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Temp/AudioImport/tools'))
sys.path.insert(0, str(ROOT / 'Temp/AudioImport/tools-soundfile'))
print('Loading audio preparation tools...', flush=True)
import numpy as np
print('Numeric processing ready', flush=True)
import soundfile as sf
print('Audio decoding ready', flush=True)

RAW = ROOT / 'Temp/AudioImport'
OUT = ROOT / 'Assets/AttackOnTroy/Audio/SFX'
RATE = 44100
SOURCES = {
    'kenney_rpg': ('Kenney', 'https://kenney.nl/assets/rpg-audio'),
    'kenney_impact': ('Kenney', 'https://kenney.nl/assets/impact-sounds'),
    'kenney_interface': ('Kenney', 'https://kenney.nl/assets/interface-sounds'),
    'kenney_jingles': ('Kenney', 'https://kenney.nl/assets/music-jingles'),
    'artisticdude_rpg': ('artisticdude', 'https://opengameart.org/content/rpg-sound-pack'),
    'artisticdude_battle': ('artisticdude (submitted by Ogrebane)', 'https://opengameart.org/content/battle-sound-effects'),
    'haeldb_vocals': ('HaelDB', 'https://opengameart.org/content/male-gruntyelling-sounds'),
    'jasinski_beach': ('jasinski (submitted by qubodup)', 'https://opengameart.org/content/beach-ocean-waves'),
    'randommind_water': ('RandomMind', 'https://opengameart.org/content/sea-and-river-wave-sounds'),
    'antumdeluge_fire': ('AntumDeluge', 'https://opengameart.org/content/fire-crackling'),
    'luke_wind': ('Luke.RUSTLTD', 'https://opengameart.org/content/wind1'),
    'ezduzziteh_explosions': ('EZduzziteh', 'https://opengameart.org/content/explosions-4'),
}
files = []
sounds = []

def find(source, pattern):
    candidates = sorted(p for p in (RAW/source).rglob(pattern) if p.is_file() and not p.name.startswith('._'))
    if not candidates:
        raise FileNotFoundError(f'{source}/{pattern}')
    return candidates[0]

def read(path, stereo=False, speed=1., limit=None):
    if path.suffix.lower() == '.mp3':
        ffmpeg = next((RAW/'tools-ffmpeg').rglob('ffmpeg*.exe'))
        decoded = path.with_suffix('.decoded.wav')
        args = [str(ffmpeg), '-hide_banner', '-loglevel', 'error', '-y', '-i', str(path)]
        if limit: args.extend(['-t', str(limit)])
        args.extend(['-ar', str(RATE), '-ac', '2', '-c:a', 'pcm_s16le', str(decoded)])
        subprocess.run(args, check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        data, rate = sf.read(str(decoded), dtype='float32', always_2d=True)
    else:
        data, rate = sf.read(str(path), dtype='float32', always_2d=True)
    if limit:
        data = data[:int(limit * rate)]
    if not stereo:
        data = data.mean(axis=1, keepdims=True)
    elif data.shape[1] == 1:
        data = np.repeat(data, 2, axis=1)
    new_count = int(len(data) * RATE / rate / speed)
    if new_count != len(data):
        axis = np.linspace(0, len(data)-1, new_count)
        data = np.column_stack([np.interp(axis, np.arange(len(data)), data[:, c]) for c in range(data.shape[1])])
    data -= data.mean(axis=0)
    return data.astype(np.float32)

def write(name, source, pattern, folder, loop=False, speed=1., limit=None, layer=None, fade_in=.008, highpass=0.):
    print('Preparing ' + name, flush=True)
    path = find(source, pattern)
    data = read(path, loop, speed, limit)
    used = [(source, path)]
    if layer:
        extra_source, extra_pattern, gain = layer
        extra_path = find(extra_source, extra_pattern)
        extra = read(extra_path, loop, speed, limit)
        count = max(len(data), len(extra))
        mixed = np.zeros((count, data.shape[1]), dtype=np.float32)
        mixed[:len(data)] += data
        mixed[:len(extra)] += extra * gain
        data = mixed
        used.append((extra_source, extra_path))
    if highpass:
        # Remove inaudible sub-bass before normalization; keep the physical attack.
        alpha = 1. / (1. + 2. * np.pi * highpass / RATE)
        previous_input = data[0].copy()
        previous_output = np.zeros(data.shape[1], dtype=np.float32)
        for i in range(len(data)):
            current = data[i].copy()
            previous_output = alpha * (previous_output + current - previous_input)
            data[i] = previous_output
            previous_input = current
    peak = float(np.max(np.abs(data)))
    if peak < 1e-6:
        raise ValueError(f'Silent recording: {path}')
    if loop:
        overlap = min(int(RATE * .3), len(data)//5)
        t = np.linspace(0., 1., overlap)[:, None]
        seam = data[-overlap:] * (1.-t) + data[:overlap] * t
        data = np.concatenate((data[overlap:-overlap], seam))
    else:
        active = np.flatnonzero(np.max(np.abs(data), axis=1) > peak * .012)
        margin = int(.005 * RATE)
        data = data[max(0, int(active[0])-margin):min(len(data), int(active[-1])+margin+1)]
        attack = min(int(RATE*fade_in), len(data)//4)
        release = min(int(RATE*.008), len(data)//4)
        data[:attack] *= np.linspace(0, 1, attack)[:, None]
        data[-release:] *= np.linspace(1, 0, release)[:, None]
    data *= .5 / max(float(np.max(np.abs(data))), 1e-6)
    target = OUT / folder / (name + ('.ogg' if loop else '.wav'))
    target.parent.mkdir(parents=True, exist_ok=True)
    if loop:
        intermediate = RAW/(name+'.prepared.wav')
        sf.write(str(intermediate), data, RATE, subtype='PCM_16')
        ffmpeg = next((RAW/'tools-ffmpeg').rglob('ffmpeg*.exe'))
        subprocess.run([str(ffmpeg),'-hide_banner','-loglevel','error','-y','-i',str(intermediate),
            '-c:a','libvorbis','-q:a','5',str(target)],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
    else:
        sf.write(str(target), data, RATE, subtype='PCM_16')
    asset_path = target.relative_to(ROOT).as_posix()
    files.append({'assetPath': asset_path, 'loop': loop, 'duration': round(len(data)/RATE, 4),
        'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
        'sources': [{'pack': s, 'originalFile': p.relative_to(RAW/s).as_posix(),
            'author': SOURCES[s][0], 'url': SOURCES[s][1], 'license': 'CC0-1.0'} for s, p in used],
        'changes': f'DC removal, 44.1 kHz, {"seam crossfade/stereo" if loop else f"trim/{fade_in*1000:g}ms attack/8ms release/mono"}, peak -6 dBFS, playback speed {speed}' + (f'; high-pass {highpass:g} Hz' if highpass else '') + (f'; layered {layer[1]} at gain {layer[2]}' if layer else '')})
    return asset_path

def cue(name, clips, bus='Combat', volume=.45, pitch=(.97,1.03), interval=0., priority=100, loop=False):
    sounds.append(dict(id=name, clips=clips, bus=bus, volume=volume, pitchMin=pitch[0], pitchMax=pitch[1], minimumInterval=interval, priority=priority, loop=loop))

def variants(name, source, patterns, folder, **kwargs):
    return [write(f'{name}_{i+1:02}', source, pattern, folder, **kwargs) for i, pattern in enumerate(patterns)]

def prepare_impacts():
    # A dry bite/wood/metal attack stays audible on small speakers, over a weighty thud.
    for name, base, extra_source, extras, gain, volume in [
        ('FleshImpact', 'impactPunch_heavy_', 'artisticdude_rpg',
         ['bite-small.wav','bite-small2.wav','bite-small3.wav'], 1., .68),
        ('GroundImpact', 'impactGeneric_light_', 'artisticdude_rpg',
         ['wood-small.wav']*3, 1., .72),
        ('StoneImpact', 'impactMining_', 'kenney_impact',
         [f'impactMetal_light_{i:03}.ogg' for i in range(3)], .9, .68),
    ]:
        clips = [write(f'{name}_{i+1:02}', 'kenney_impact', f'{base}{i:03}.ogg',
                       'Weapons', speed=(.97,1.,1.03)[i], limit=.5,
                       layer=(extra_source, extras[i], gain), fade_in=.002, highpass=140.)
                 for i in range(3)]
        cue(name, clips, volume=volume, priority=70)

if '--impacts-only' in sys.argv:
    manifest_path = OUT/'sound-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    prepare_impacts()
    replacement_files = {entry['assetPath']: entry for entry in files}
    replacement_sounds = {entry['id']: entry for entry in sounds}
    manifest['files'] = [replacement_files.get(entry['assetPath'], entry) for entry in manifest['files']]
    manifest['sounds'] = [replacement_sounds.get(entry['id'], entry) for entry in manifest['sounds']]
    manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print(f'Updated only {len(files)} impact clips and {len(sounds)} cue definitions.')
    sys.exit(0)

swings = variants('SpearLaunch', 'artisticdude_rpg', ['swing.wav','swing2.wav','swing3.wav'], 'Weapons')
cue('SpearLaunch', swings, volume=.52)
bow = [write(f'ArrowLaunch_{i+1:02}', 'artisticdude_battle', 'Bow.wav', 'Weapons', speed=speed) for i,speed in enumerate((.97,1.,1.03))]
cue('ArrowLaunch', bow, volume=.48, pitch=(.99,1.01))
heavy_swings = variants('HeavyThrow', 'artisticdude_rpg', ['swing.wav','swing2.wav','swing3.wav'], 'Weapons', speed=.8)
cue('HeavyThrow', heavy_swings, volume=.52, pitch=(.92,.99))
cue('AxeLaunch', heavy_swings, volume=.58, pitch=(.85,.93))
cue('HeavyAxeLaunch', heavy_swings, volume=.62, pitch=(.7,.8))
prepare_impacts()
cue('BombExplosion',variants('BombExplosion','ezduzziteh_explosions',['explosion1_0.ogg','explosion2.ogg','explosion3.ogg'],'Weapons',limit=2.5),volume=.65,pitch=(.92,1.02),priority=35)
cue('JarBreak',[write(f'JarBreak_{i+1:02}','kenney_impact',f'impactGlass_light_{i:03}.ogg','Weapons',layer=('kenney_impact',f'impactWood_heavy_{i:03}.ogg',.8)) for i in range(3)],volume=.5,priority=70)
fire = write('FireLoop','antumdeluge_fire','fire-1.wav','Environment',loop=True,limit=12)
cue('FireLoop',[fire],volume=.18,pitch=(.98,1.02),priority=150,loop=True)
ignite = write('FireIgnite','antumdeluge_fire','fire-1.wav','Weapons',limit=.7)
cue('FireIgnite',[ignite],volume=.3,priority=100)
charge = write('ChargeLoop','artisticdude_rpg','magic1.wav','Weapons',loop=True,limit=2.)
cue('ChargeLoop',[charge],volume=.12,pitch=(1.,1.),priority=160,loop=True)
for name, pattern, volume in [('WeaponSelect','metalClick.ogg',.3),('HumanLand','footstep00.ogg',.36)]:
    cue(name,[write(name,'kenney_rpg',pattern,'UI' if name=='WeaponSelect' else 'Characters')],volume=volume,interval=.1,priority=150)
magic = variants('Magic','artisticdude_rpg',['magic1.wav','spell.wav'],'UI',limit=1.5)
for name, clip, pitch, volume in [('EnergyReady',magic[0],1.25,.35),('SpecialArm',magic[1],1.1,.4),('SpecialCancel',magic[0],.8,.22),('Heal',magic[1],1.3,.35)]:
    cue(name,[clip],volume=volume,pitch=(pitch,pitch),interval=.15,priority=65)
foot_sand = variants('FootstepSand','kenney_impact',[f'footstep_grass_{i:03}.ogg' for i in range(3)],'Characters')
foot_stone = variants('FootstepStone','kenney_impact',[f'footstep_concrete_{i:03}.ogg' for i in range(3)],'Characters')
cue('HumanFootstepSand',foot_sand,volume=.28,interval=.15,priority=230)
cue('HumanFootstepStone',foot_stone,volume=.28,interval=.15,priority=230)
giant_steps = variants('GiantFootstep','kenney_impact',[f'impactSoft_heavy_{i:03}.ogg' for i in range(3)],'Characters',speed=.78)
cue('CyclopsFootstep',giant_steps,volume=.38,pitch=(.9,.98),interval=.2,priority=220)
cue('HeavyCyclopsFootstep',giant_steps,volume=.45,pitch=(.7,.8),interval=.2,priority=215)
cue('CyclopsLand',giant_steps,volume=.48,pitch=(.8,.9),interval=.3,priority=180)
for hero, prefix in [('Achilles','3grunt'),('Odysseus','1yell')]:
    short = variants(hero+'Grunt','haeldb_vocals',[f'{prefix}{i}.wav' for i in (1,2,3)],'Characters',limit=.7)
    hurt = variants(hero+'Hurt','haeldb_vocals',[f'{prefix}{i}.wav' for i in (4,5,6)],'Characters',limit=.8)
    death = [write(hero+'Death','haeldb_vocals', '3yell7.wav' if hero=='Achilles' else '1yell7.wav','Characters',limit=1.5)]
    cue(hero+'Attack',short,'Voices',.32,interval=3.,priority=140)
    cue(hero+'Hurt',hurt,'Voices',.42,interval=1.,priority=90)
    cue(hero+'Death',death,'Voices',.52,pitch=(1.,1.),priority=25)
giant = variants('CyclopsVoice','artisticdude_rpg',[f'giant{i}.wav' for i in (1,2,3)],'Characters',limit=1.)
giant_death = [write('CyclopsDeath','artisticdude_rpg','giant5.wav','Characters',limit=1.8)]
for name, pitch, volume in [('Cyclops',(.92,1.),.48),('HeavyCyclops',(.72,.8),.55)]:
    cue(name+'Attack',giant,'Voices',volume,pitch,interval=3.,priority=130)
    cue(name+'Hurt',giant,'Voices',volume,pitch,interval=1.,priority=85)
    cue(name+'Death',giant_death,'Voices',volume,pitch,priority=25)
for name, pattern, volume in [('UIClick','click_',.3),('UIConfirm','confirmation_',.35),('UIBack','back_',.3),('UIUnavailable','error_',.25)]:
    cue(name,variants(name,'kenney_interface',[f'{pattern}{i:03}.ogg' for i in (1,2,3)],'UI'),'UI',volume,pitch=(1.,1.),interval=.05,priority=120)
cue('PlayerTurn',[sounds[-3]['clips'][0]],'UI',.18,pitch=(1.12,1.12),interval=.3,priority=180)
victory = write('Victory','kenney_jingles','jingles_PIZZI00.ogg','UI',limit=4.)
defeat = write('Defeat','kenney_jingles','jingles_PIZZI01.ogg','UI',limit=3.)
cue('Victory',[victory],'UI',.55,pitch=(1.,1.),interval=1.,priority=10)
cue('Defeat',[defeat],'UI',.5,pitch=(.88,.88),interval=1.,priority=10)
cue('SplashSting',[magic[0]],'UI',.22,pitch=(.9,.9),interval=1.,priority=100)
for name, source, pattern, limit in [('BeachAmbience','jasinski_beach','wave_01*.flac',24.),('RiverAmbience','randommind_water','VistulaShort_0.mp3',24.),('WallAmbience','luke_wind','wind1.wav',24.)]:
    cue(name,[write(name,source,pattern,'Environment',loop=True,limit=limit)],'Ambience',.65,pitch=(1.,1.),priority=10,loop=True)

manifest = {'sounds': sounds, 'files': files}
(OUT/'sound-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
used_sources = sorted({entry['pack'] for f in files for entry in f['sources']})
credits = ['ATTACK ON TROY - SOUND EFFECT CREDITS','',
    'New SFX recordings: CC0 1.0 (selected from multi-license sources under CC0).',
    'https://creativecommons.org/publicdomain/zero/1.0/',
    'Edits: trimming, fades, resampling, normalization, looping and layering.',
    'Existing menu and battle music are unchanged and are not covered by this SFX notice.','']
for source in used_sources:
    author,url=SOURCES[source]
    credits.extend([author+' - '+source, url, 'License: CC0 1.0',''])
(OUT/'AudioCredits.txt').write_text('\n'.join(credits),encoding='utf-8')
license_dir=OUT/'Licenses'
license_dir.mkdir(exist_ok=True)
for source in used_sources:
    if source.startswith('kenney_'):
        candidates=[p for p in (RAW/source).rglob('*.txt') if 'license' in p.name.lower()]
        if candidates:
            (license_dir/(source+'.txt')).write_bytes(candidates[0].read_bytes())
    else:
        author,url=SOURCES[source]
        (license_dir/(source+'.txt')).write_text(f'{author}\n{url}\nSelected license: CC0 1.0\nhttps://creativecommons.org/publicdomain/zero/1.0/\n',encoding='utf-8')
print(f'{len(sounds)} sound events, {len(files)} prepared clips, {sum((ROOT/f["assetPath"]).stat().st_size for f in files)/1048576:.2f} MiB')

# Native Unity AudioMixer serialization, imported/validated by the Editor setup.
def guid(value): return hashlib.sha256(('AttackOnTroyAudio:' + value).encode()).hexdigest()[:32]
groups = [('Master',0),('Music',-18),('Combat',4),('Voices',2),('UI',-2),('Ambience',-30)]
header = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
common = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
mixer = header + '--- !u!241 &24100000\nAudioMixerController:\n' + common + '  m_Name: MX_GameAudio\n  m_OutputGroup: {fileID: 0}\n  m_MasterGroup: {fileID: 24300000}\n  m_Snapshots:\n  - {fileID: 24500000}\n  m_StartSnapshot: {fileID: 24500000}\n  m_SuspendThreshold: -80\n  m_EnableSuspend: 1\n  m_UpdateMode: 1\n  m_ExposedParameters:\n'
for name in ['Music','Ambience','Combat','Voices','UI']:
    mixer += f'  - guid: {guid(name+"volume")}\n    name: {name}Volume\n'
mixer += '  m_AudioMixerGroupViews:\n  - guids:\n'
for name,_ in groups: mixer += f'    - {guid(name+"group")}\n'
mixer += '    name: View\n  m_CurrentViewIndex: 0\n  m_TargetSnapshot: {fileID: 24500000}\n'
for i,(name,db) in enumerate(groups):
    mixer += f'--- !u!243 &{24300000+i}\nAudioMixerGroupController:\n' + common
    mixer += f'  m_Name: {name}\n  m_AudioMixer: {{fileID: 24100000}}\n  m_GroupID: {guid(name+"group")}\n'
    if i == 0:
        mixer += '  m_Children:\n' + ''.join(f'  - {{fileID: {24300000+j}}}\n' for j in range(1,len(groups)))
    else: mixer += '  m_Children: []\n'
    mixer += f'  m_Volume: {guid(name+"volume")}\n  m_Pitch: {guid(name+"pitch")}\n  m_Send: 00000000000000000000000000000000\n  m_Effects:\n  - {{fileID: {24400000+i}}}\n  m_UserColorIndex: 0\n  m_Mute: 0\n  m_Solo: 0\n  m_BypassEffects: 0\n'
    mixer += f'--- !u!244 &{24400000+i}\nAudioMixerEffectController:\n' + common
    mixer += f'  m_Name: \n  m_EffectID: {guid(name+"attenuation")}\n  m_EffectName: Attenuation\n  m_MixLevel: {guid(name+"mix")}\n  m_Parameters: []\n  m_SendTarget: {{fileID: 0}}\n  m_EnableWetMix: 0\n  m_Bypass: 0\n'
mixer += '--- !u!245 &24500000\nAudioMixerSnapshotController:\n' + common
mixer += '  m_Name: Default\n  m_AudioMixer: {fileID: 24100000}\n  m_SnapshotID: '+guid('snapshot')+'\n  m_FloatValues:\n'
for name,db in groups:
    mixer += f'    {guid(name+"volume")}: {db}\n    {guid(name+"pitch")}: 1\n'
mixer += '  m_TransitionOverrides: {}\n'
mixer_path = ROOT/'Assets/AttackOnTroy/Audio/Mixers/MX_GameAudio.mixer'
mixer_path.parent.mkdir(parents=True,exist_ok=True)
mixer_path.write_text(mixer,encoding='utf-8')
