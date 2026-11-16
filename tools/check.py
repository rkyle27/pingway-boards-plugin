#!/usr/bin/env python3
"""Checks the Pingway Boards plugin files that can be checked without .NET:
- every JSON file parses (manifest, repo.json, packages.lock.json, Lang/*.json, Demo/demo-posters.json)
- manifest + repo.json have the fields Dalamud needs, the same version as the .csproj, API level 15
- the 4 language files have exactly the same keys, no empty texts, the same {placeholders}
- every text key the C# code asks for exists
- the demo posters follow the feed format v1 (FEED-v1): fields, types, kinds, https links, pingway.app urls
- the PNG assets the code uses exist, are real PNGs and not too big; the icon is square and at most 512 px
Run: python3 tools/check.py   (no packages needed)
"""
import json, os, re, struct, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
P = os.path.join(ROOT, 'PingwayBoards')
errors = []


def err(msg):
    errors.append(msg)


def load(path):
    try:
        with open(path, encoding='utf-8') as f:
            return json.load(f)
    except Exception as e:  # noqa: BLE001
        err('%s: not valid JSON (%s)' % (os.path.relpath(path, ROOT), e))
        return None


def png_size(path):
    with open(path, 'rb') as f:
        head = f.read(24)
    if head[:8] != b'\x89PNG\r\n\x1a\n':
        return None
    return struct.unpack('>II', head[16:24])


# ---------- version from the .csproj ----------
csproj = open(os.path.join(P, 'PingwayBoards.csproj'), encoding='utf-8').read()
m = re.search(r'<Version>([0-9.]+)</Version>', csproj)
version = m.group(1) if m else None
if not version:
    err('PingwayBoards.csproj: no <Version>')
if 'Sdk="Dalamud.NET.Sdk/15.0.0"' not in csproj:
    err('PingwayBoards.csproj: must use Dalamud.NET.Sdk/15.0.0')
if re.search(r'<PackageReference', csproj):
    err('PingwayBoards.csproj: no extra PackageReference allowed (only the SDK)')

# ---------- manifest ----------
man = load(os.path.join(P, 'PingwayBoards.json'))
if man is not None:
    for k in ('Author', 'Name', 'Punchline', 'Description', 'RepoUrl'):
        if not isinstance(man.get(k), str) or not man[k].strip():
            err('PingwayBoards.json: %s missing' % k)
    if man.get('Name') != 'Pingway Boards':
        err('PingwayBoards.json: Name must be "Pingway Boards"')
    if man.get('Author') != 'River Zola':
        err('PingwayBoards.json: Author must be "River Zola"')
    if man.get('DalamudApiLevel') != 15:
        err('PingwayBoards.json: DalamudApiLevel must be 15')
    if not isinstance(man.get('Tags'), list) or not all(isinstance(t, str) for t in man['Tags']):
        err('PingwayBoards.json: Tags must be a list of texts')
    d = man.get('Description', '')
    for must in ('Read-only', 'pingway.app', 'Square Enix'):
        if must not in d:
            err('PingwayBoards.json: Description should mention %r' % must)

# ---------- repo.json (pluginmaster) ----------
repo = load(os.path.join(ROOT, 'repo.json'))
if repo is not None:
    if not isinstance(repo, list) or len(repo) != 1:
        err('repo.json: must be a list with one plugin')
    else:
        r = repo[0]
        zip_url = 'https://github.com/rkyle27/pingway-boards-plugin/releases/latest/download/latest.zip'
        for k in ('DownloadLinkInstall', 'DownloadLinkUpdate'):
            if r.get(k) != zip_url:
                err('repo.json: %s must be %s' % (k, zip_url))
        if r.get('InternalName') != 'PingwayBoards':
            err('repo.json: InternalName must be PingwayBoards (the assembly name)')
        if r.get('AssemblyVersion') != version:
            err('repo.json: AssemblyVersion %r is not the .csproj Version %r' % (r.get('AssemblyVersion'), version))
        if r.get('DalamudApiLevel') != 15:
            err('repo.json: DalamudApiLevel must be 15')
        if not isinstance(r.get('LastUpdate'), int) or r['LastUpdate'] < 1700000000:
            err('repo.json: LastUpdate must be a Unix time in seconds')
        if man is not None:
            for k in ('Name', 'Author', 'Punchline', 'Description', 'RepoUrl', 'Tags', 'IconUrl'):
                if r.get(k) != man.get(k):
                    err('repo.json: %s differs from PingwayBoards.json' % k)
        icon_url = r.get('IconUrl', '')
        prefix = 'https://raw.githubusercontent.com/rkyle27/pingway-boards-plugin/main/'
        if not icon_url.startswith(prefix) or not os.path.isfile(os.path.join(ROOT, icon_url[len(prefix):])):
            err('repo.json: IconUrl must point to a file in this repo (main branch)')

# ---------- lock file ----------
lock = load(os.path.join(P, 'packages.lock.json'))
if lock is not None:
    pk = set()
    for tfm in lock.get('dependencies', {}).values():
        pk |= set(tfm.keys())
    if pk - {'DalamudPackager', 'DotNet.ReproducibleBuilds'}:
        err('packages.lock.json: unexpected packages %s' % sorted(pk - {'DalamudPackager', 'DotNet.ReproducibleBuilds'}))

# ---------- languages ----------
LANGS = ('en', 'fr', 'de', 'ja')
langs = {}
for L in LANGS:
    d = load(os.path.join(P, 'Lang', L + '.json'))
    if d is not None:
        if not all(isinstance(v, str) and v.strip() for v in d.values()):
            err('Lang/%s.json: empty or non-text values' % L)
        langs[L] = d
if len(langs) == 4:
    base = set(langs['en'])
    for L in LANGS[1:]:
        if set(langs[L]) != base:
            err('Lang/%s.json: keys differ from en (missing %s, extra %s)' % (L, sorted(base - set(langs[L]))[:8], sorted(set(langs[L]) - base)[:8]))
    ph = lambda s: sorted(set(re.findall(r'\{(\w+)\}', s)))
    for k in base:
        for L in LANGS[1:]:
            if k in langs[L] and ph(langs['en'][k]) != ph(langs[L][k]):
                err('Lang/%s.json: %s has placeholders %s, en has %s' % (L, k, ph(langs[L][k]), ph(langs['en'][k])))

# ---------- every key the C# code uses ----------
cs_files = []
for dirpath, _, files in os.walk(P):
    if os.sep + 'bin' in dirpath or os.sep + 'obj' in dirpath:
        continue
    cs_files += [os.path.join(dirpath, f) for f in files if f.endswith('.cs')]
cs = '\n'.join(open(f, encoding='utf-8').read() for f in cs_files)
if 'en' in langs:
    # keys ending in "." are prefixes completed in the code from the lists below
    used = {k for k in re.findall(r'\.T\("([a-zA-Z0-9_.]+)"', cs) if not k.endswith('.')}
    # every "bd.…" / "pb.…" text in the code (also the ones picked with ? :)
    used |= {k for k in re.findall(r'"((?:bd|pb)\.[a-zA-Z0-9_.]+)"', cs) if not k.endswith('.') and k != 'bd.count'}
    used |= {k + '_one' for k in re.findall(r'\.Count\("([a-zA-Z0-9_.]+)"', cs)} | {k + '_other' for k in re.findall(r'\.Count\("([a-zA-Z0-9_.]+)"', cs)}
    # keys built from lists: "bd.f.sort." + k, "bd.c." + id, "job." + id, "bd.role." + id, "bd.tag." + id, "region." + key
    for k in ('soon', 'az', 'za', 'new', 'old'):
        used.add('bd.f.sort.' + k)
    for c in ('ucob', 'uwu', 'tea', 'dsr', 'top', 'fru', 'dmu', 'm9s', 'm10s', 'm11s', 'm12s', 'other'):
        used.add('bd.c.' + c)
    for j in ('pld', 'war', 'drk', 'gnb', 'whm', 'sch', 'ast', 'sge', 'mnk', 'drg', 'nin', 'sam', 'rpr', 'vpr', 'brd', 'mch', 'dnc', 'blm', 'smn', 'rdm', 'pct'):
        used.add('job.' + j)
    for r in ('tank', 'healer', 'melee', 'ranged', 'caster'):
        used.add('bd.role.' + r)
    for t in ('raid', 'newbie', 'casual', 'hardcore', 'chill', 'events', 'crafting', 'gathering', 'housing', 'pvp', 'roleplay', 'mentor'):
        used.add('bd.tag.' + t)
    for r in ('na', 'eu', 'oce', 'jp'):
        used.add('region.' + r)
    missing = sorted(k for k in used if k not in langs['en'])
    if missing:
        err('Lang/en.json: keys used by the code are missing: %s' % missing)
    if re.search(r'\bPingway\b', cs) is None:
        err('code: expected the plugin name in the code')

# ---------- network: the only address in the code ----------
urls = {u.rstrip('.,;:') for u in re.findall(r'https?://[^\s"\')]+', cs)}
allowed = {'https://pingway.app/api/boards-feed', 'https://pingway.app/'}
if urls - allowed:
    err('code: unexpected web addresses %s' % sorted(urls - allowed))
for bad in ('io.popen', 'Process.Start', 'WebClient', 'curl', 'Telemetry'):
    if bad in cs:
        err('code: %s should not be used' % bad)

# ---------- demo posters (feed v1) ----------
STR = ('kind', 'title', 'description', 'host', 'playtime', 'region', 'dc', 'world', 'schedule', 'ownerName', 'url')
LISTS = ('worlds', 'content', 'tags', 'roles', 'jobs')
NUMS = ('id', 'eventAt', 'createdAt', 'bumpedAt')
KINDS = {'lfm', 'lfs', 'fc', 'lffc', 'event'}
CONTENT = {'ucob', 'uwu', 'tea', 'dsr', 'top', 'fru', 'dmu', 'm9s', 'm10s', 'm11s', 'm12s', 'other'}
TAGS = {'raid', 'newbie', 'casual', 'hardcore', 'chill', 'events', 'crafting', 'gathering', 'housing', 'pvp', 'roleplay', 'mentor'}
ROLES = {'tank', 'healer', 'melee', 'ranged', 'caster'}
JOBS = {'pld', 'war', 'drk', 'gnb', 'whm', 'sch', 'ast', 'sge', 'mnk', 'drg', 'nin', 'sam', 'rpr', 'vpr', 'brd', 'mch', 'dnc', 'blm', 'smn', 'rdm', 'pct'}
REGIONS = {'North America (NA)', 'Europe (EU)', 'Oceania (OCE)', 'Japan (JP)'}
NEVER = ('ownerId', 'signups', 'reports', 'image', 'hidden', 'expiresAt', 'starredBy')
demo = load(os.path.join(P, 'Demo', 'demo-posters.json'))
if demo is not None:
    if demo.get('v') != 1 or not isinstance(demo.get('generated'), int) or not isinstance(demo.get('posters'), list):
        err('demo: needs v = 1, generated (number) and posters (list)')
    ids = set()
    kinds_seen = set()
    featured = {}
    for p in demo.get('posters', []):
        pid = p.get('id')
        where = 'demo poster %s' % pid
        for k in STR:
            if not isinstance(p.get(k), str):
                err('%s: %s must be text' % (where, k))
        for k in LISTS:
            if not isinstance(p.get(k), list) or not all(isinstance(x, str) for x in p[k]):
                err('%s: %s must be a list of texts' % (where, k))
        for k in NUMS:
            if not isinstance(p.get(k), int):
                err('%s: %s must be a number' % (where, k))
        if not isinstance(p.get('links'), list) or len(p['links']) > 3:
            err('%s: links must be a list of at most 3' % where)
        else:
            for l in p['links']:
                if not isinstance(l, dict) or not isinstance(l.get('title'), str) or not str(l.get('url', '')).startswith('https://'):
                    err('%s: every link needs a title and an https:// url' % where)
        for k in NEVER:
            if k in p:
                err('%s: %s must never be in the feed' % (where, k))
        kind = p.get('kind')
        kinds_seen.add(kind)
        if kind not in KINDS:
            err('%s: unknown kind %r' % (where, kind))
        if pid in ids:
            err('%s: duplicate id' % where)
        ids.add(pid)
        if not str(p.get('url', '')).startswith('https://pingway.app/'):
            err('%s: url must start with https://pingway.app/' % where)
        if p.get('region') not in REGIONS:
            err('%s: unknown region' % where)
        if not set(p.get('content', [])) <= CONTENT or not set(p.get('tags', [])) <= TAGS or not set(p.get('roles', [])) <= ROLES or not set(p.get('jobs', [])) <= JOBS:
            err('%s: unknown content / tag / role / job' % where)
        if kind == 'event':
            if not isinstance(p.get('stars'), int) or not isinstance(p.get('featured'), int) or p['featured'] not in (0, 1, 2, 3):
                err('%s: events need stars and featured (0-3)' % where)
            if p.get('eventAt', 0) <= 0:
                err('%s: demo events must be in the future (eventAt > 0)' % where)
            if not p.get('host'):
                err('%s: events need a host' % where)
            if p.get('featured'):
                featured[p['featured']] = p
        else:
            if p.get('eventAt') != 0 or p.get('host') != '':
                err('%s: eventAt must be 0 and host "" for non-events' % where)
            if 'stars' in p or 'featured' in p:
                err('%s: stars / featured are for events only' % where)
        if kind == 'fc' and not p.get('world'):
            err('%s: fc needs a world' % where)
        if kind == 'lffc' and p.get('world'):
            err('%s: lffc uses worlds, not world' % where)
        if kind != 'lffc' and (p.get('worlds') or p.get('playtime')):
            err('%s: worlds / playtime are for lffc only' % where)
        if p.get('createdAt', 0) > 0:
            err('%s: demo createdAt must be in the past (<= 0)' % where)
        if 'River Zola' in json.dumps(p, ensure_ascii=False):
            err('%s: use fake names only' % where)
    if kinds_seen != KINDS:
        err('demo: should have every kind (has %s)' % sorted(k for k in kinds_seen if k))
    # featured = the top 3 events by stars (more than 100)
    ev = sorted((p for p in demo.get('posters', []) if p.get('kind') == 'event' and p.get('stars', 0) > 100), key=lambda p: (-p['stars'], p['eventAt']))[:3]
    want = {i + 1: p['id'] for i, p in enumerate(ev)}
    if {k: v['id'] for k, v in featured.items()} != want:
        err('demo: featured ranks %s do not match the top 3 by stars %s' % ({k: v['id'] for k, v in featured.items()}, want))

# ---------- assets ----------
used_png = set(re.findall(r'"([a-z_]+\.png)"', cs))
used_png |= {'poster_event_%s.png' % t for t in ('pink', 'mint', 'lemon', 'lilac', 'sky')}
total = 0
for name in sorted(used_png):
    path = os.path.join(P, 'Assets', name)
    if not os.path.isfile(path):
        err('Assets/%s: missing' % name)
        continue
    size = png_size(path)
    if size is None:
        err('Assets/%s: not a PNG' % name)
    total += os.path.getsize(path)
    if os.path.getsize(path) > 600 * 1024:
        err('Assets/%s: bigger than 600 KB' % name)
if total > 2 * 1024 * 1024:
    err('Assets: more than 2 MB in total')
for name in os.listdir(os.path.join(P, 'Assets')):
    if name not in used_png:
        err('Assets/%s: not used by the code' % name)
icon = os.path.join(P, 'images', 'icon.png')
isz = png_size(icon) if os.path.isfile(icon) else None
if isz is None or isz[0] != isz[1] or isz[0] > 512 or isz[0] < 64:
    err('images/icon.png: must be a square PNG, 64 to 512 px (is %s)' % (isz,))
for n in ('poster_lfm.png', 'poster_lfs.png', 'poster_fc.png', 'poster_event_pink.png'):
    path = os.path.join(P, 'Assets', n)
    if os.path.isfile(path) and png_size(path) != (308, 442):
        err('Assets/%s: blank posters must be 308 x 442 (the code expects that)' % n)

# ---------- result ----------
if errors:
    print('FAILED (%d):' % len(errors))
    for e in errors:
        print(' -', e)
    sys.exit(1)
print('OK: manifest, repo.json, lock file, %d language keys x 4, %d demo posters, %d assets (%d KB), icon %s' % (
    len(langs.get('en', {})), len((demo or {}).get('posters', [])), len(used_png), total // 1024, isz))
