# Renders the blank Pingway Boards backgrounds (from the website's own Boards CSS) to PNGs for the plugin.
# Usage: python3 -I tools/render_assets.py <copy of the website public folder> <raw output folder>   (needs Python Playwright + Chromium)
# It serves the copy on 127.0.0.1 only, blocks every other address, and hides all text so only the art is captured.
import json, os, socket, subprocess, sys, time
from playwright.sync_api import sync_playwright

SITE, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)


def free_port():
    s = socket.socket(); s.bind(('127.0.0.1', 0)); p = s.getsockname()[1]; s.close(); return p


def api(route, req):
    path = req.url.split('?')[0].split('/api/')[1]
    if path == 'env':
        return route.fulfill(status=200, content_type='application/json', body=json.dumps({'preview': True}))
    return route.fulfill(status=401, content_type='application/json', body=json.dumps({'error': 'Please log in with Discord.'}))


# Only the art stays visible: every text, control and the rest of the page is hidden (visibility also hides backgrounds)
ART_ONLY_CSS = """
html, body { background: transparent !important; }
body * { visibility: hidden !important; }
[data-board], .bd-cork-in, .bd-glass-in, .bd-chalk-in,
.bd-worn, .bd-worn *, .bd-doodles, .bd-doodles *, .bd-tray, .bd-tray * { visibility: visible !important; }
#pw-host, #pw-host * { visibility: visible !important; }
#pw-host .pw-hide, #pw-host .pw-hide * { visibility: hidden !important; }
.bd-dd path, .bd-dd ellipse, .bd-dd circle { animation: none !important; stroke-dasharray: none !important; }
* { transition: none !important; animation: none !important; }
"""


def main():
    port = free_port()
    srv = subprocess.Popen([sys.executable, '-m', 'http.server', str(port), '-d', SITE, '--bind', '127.0.0.1'],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(0.8)
    try:
        with sync_playwright() as p:
            b = p.chromium.launch()
            ctx = b.new_context(viewport={'width': 1400, 'height': 1000}, device_scale_factor=1, locale='en-US', reduced_motion='reduce')
            # block anything that is not our local server
            ctx.route(lambda u: not u.startswith('http://127.0.0.1:%d' % port), lambda r, q: r.abort())
            ctx.route('**/api/**', api)
            ctx.add_init_script("try { localStorage.setItem('pw-lang','en'); localStorage.setItem('pw-daynight', JSON.stringify('night')); localStorage.setItem('pw-effects','false'); } catch (e) {}")
            page = ctx.new_page()
            page.goto('http://127.0.0.1:%d/' % port)
            page.wait_for_selector('#boards .bd-poster', timeout=15000)
            page.wait_for_timeout(500)
            page.add_style_tag(content=ART_ONLY_CSS)
            jsclick = lambda sel: page.evaluate("(s) => document.querySelector(s).click()", sel)

            def board(kind):
                jsclick('[data-board-tab="%s"]' % kind)
                page.wait_for_selector('[data-board="%s"]' % kind, state='attached')
                page.wait_for_timeout(300)

            def shoot_board(kind, name, w, h):
                board(kind)
                page.evaluate("""([kind, w, h]) => {
                    const el = document.querySelector('[data-board="' + kind + '"]');
                    el.style.width = w + 'px'; el.style.height = h + 'px'; el.style.boxSizing = 'border-box'; el.style.boxShadow = 'none';
                    el.style.position = 'fixed'; el.style.left = '20px'; el.style.top = '20px'; el.style.zIndex = '99999'; el.style.margin = '0';
                    const inn = el.querySelector('.bd-cork-in, .bd-glass-in, .bd-chalk-in');
                    inn.style.height = '100%'; inn.style.boxSizing = 'border-box';
                }""", [kind, w, h])
                page.wait_for_timeout(200)
                page.locator('[data-board="%s"]' % kind).screenshot(path=os.path.join(OUT, name), omit_background=True)

            def stage(html_fn_js, arg):
                page.evaluate("""([fn, arg]) => {
                    const host = document.createElement('div');
                    host.id = 'pw-host';
                    host.style.cssText = 'position:fixed;left:0;top:0;padding:30px;background:transparent;z-index:99999;display:flex';
                    document.body.appendChild(host);
                    (new Function('host', 'arg', fn))(host, arg);
                }""", [html_fn_js, arg])
                page.wait_for_timeout(150)

            def unstage():
                page.evaluate("() => document.getElementById('pw-host').remove()")

            shoot_board('static', 'board_static.png', 960, 720)
            shoot_board('fc', 'board_fc.png', 960, 720)
            shoot_board('event', 'board_event.png', 960, 720)

            # the Static sign plank without its title (the plugin writes the title over it)
            board('static')
            stage("""
                const c = document.querySelector('.bd-sign').cloneNode(true);
                const h = c.querySelector('h3'); h.textContent = 'Static recruitment board'; h.classList.add('pw-hide');
                c.style.transform = 'none'; c.style.margin = '0'; c.id = 'pw-shot';
                host.appendChild(c);
            """, None)
            page.locator('#pw-shot').screenshot(path=os.path.join(OUT, 'sign_static.png'), omit_background=True)
            unstage()

            # Blank posters: the website's own poster markup, text hidden, no tilt, on a transparent stage
            POSTER_JS = """
                const [sel, w, h] = arg;
                const c = document.querySelector(sel).cloneNode(true);
                c.style.setProperty('--tilt', '0deg');
                c.style.transform = 'none';
                c.style.width = w + 'px'; c.style.height = h + 'px'; c.style.flex = 'none';
                c.classList.remove('bd-featured');
                for (const k of Array.from(c.querySelectorAll('.bd-ribbon, .bd-fc-img, .bd-mark'))) k.remove();
                const keep = ['bd-pin', 'bd-roll', 'bd-tape', 'bd-crease'];
                const inner = c.querySelector('.bd-paper, .bd-inv-in, .bd-fc-body') || c;
                for (const k of Array.from(inner.children)) if (!keep.some(x => k.classList.contains(x))) k.classList.add('pw-hide');
                const body = c.querySelector('.bd-fc-body'); if (body) body.style.flex = '1';
                c.id = 'pw-shot';
                host.appendChild(c);
            """

            def shoot_poster(sel, name, w=280, h=400):
                stage(POSTER_JS, [sel, w, h])
                page.locator('#pw-host').screenshot(path=os.path.join(OUT, name), omit_background=True)
                unstage()

            board('static')
            jsclick('[data-stab=lfm]'); page.wait_for_timeout(300)
            shoot_poster('.bd-p-lfm', 'poster_lfm.png')
            jsclick('[data-stab=lfs]'); page.wait_for_timeout(300)
            shoot_poster('.bd-p-lfs', 'poster_lfs.png')
            board('fc')
            shoot_poster('.bd-fc', 'poster_fc.png')
            board('event')
            for tone in ['pink', 'mint', 'lemon', 'lilac', 'sky']:
                page.evaluate("""(tone) => { const s = document.querySelector('.bd-invite'); s.className = s.className.replace(/bd-t-[a-z]+/, 'bd-t-' + tone); }""", tone)
                shoot_poster('.bd-invite', 'poster_event_%s.png' % tone)
            b.close()
    finally:
        srv.terminate()


main()
