// BONK BRAWL page side: online lobby + wins leaderboard + the WebSocket pipe.
// During a fight Unity hands us input/hash packets (window.brawl.send) and we hand the other players' packets
// straight back (SendMessage "OnNet"). The rollback logic lives in Unity; the server only relays.
(function () {
  var qs = new URLSearchParams(location.search);
  var API = qs.get("api") || "https://orbyt-api-production-29f6.up.railway.app";
  var WS_URL = API.replace(/^http/, "ws") + "/brawl";
  var STAGES = ["RANDOM", "NEON ROOFTOP", "MOONLIT CRYPT", "MOON BASE", "THE PASS", "HAUNTED HOLLOW"];
  var LEVELS = ["", "EASY", "NORMAL", "HARD"];
  var meName = store("bb_name"), myCh = 0, ws = null, fighting = false, wantJoin = qs.get("brawl"), cfg = { stage: -1, bots: 0, botLv: 2 };

  function store(k, v) { try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); } catch (e) { return null; } }
  function playerId() {
    var id = store("bb_player");
    if (!id) { id = crypto.randomUUID ? crypto.randomUUID() : (Date.now().toString(16) + Math.random().toString(16).slice(2)); store("bb_player", id); }
    return id;
  }
  function toUnityRaw(json) { try { window.unityInstance && window.unityInstance.SendMessage("Game", "OnNet", json); } catch (e) {} }
  function toUnity(method, payload) { try { window.unityInstance && window.unityInstance.SendMessage("Game", method, JSON.stringify(payload)); } catch (e) {} }
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); }
  function toast(m) { window.bbToast && window.bbToast(m); }
  function track(n, v) { window.SD && window.SD.track && window.SD.track(n, v || 0); }
  function sendWs(m) { if (ws && ws.readyState === 1) ws.send(typeof m === "string" ? m : JSON.stringify(m)); }

  // ---------------------------------------------------------------- styles
  var css = document.createElement("style");
  css.textContent = [
    ".bbov{position:fixed;inset:0;z-index:20;display:none;align-items:center;justify-content:center;background:rgba(12,6,28,.8);backdrop-filter:blur(3px);font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;color:#fff}",
    ".bbov.on{display:flex}",
    ".bbov .card{width:min(440px,92vw);max-height:88vh;display:flex;flex-direction:column;background:#1d1238;border:1px solid rgba(255,255,255,.14);border-radius:24px;box-shadow:0 20px 60px rgba(0,0,0,.55);overflow:hidden;animation:bbin .25s ease}",
    "@keyframes bbin{from{transform:scale(.88);opacity:0}}",
    ".bbov .hd{padding:18px 18px 8px;display:flex;align-items:center;justify-content:space-between}",
    ".bbov h2{margin:0;font-size:28px;font-weight:900;font-style:italic;letter-spacing:1px;color:#ffd23f;text-shadow:0 3px 0 #b31e5a}",
    ".bbov .x{background:rgba(255,255,255,.1);border:0;color:#fff;width:38px;height:38px;border-radius:12px;font-size:18px;cursor:pointer}",
    ".bbov .body{padding:4px 22px 22px;text-align:center;overflow:auto}",
    ".bbov .sub{opacity:.75;font-size:14px;margin:0 0 14px}",
    ".bbov .btn{display:block;width:100%;padding:16px;margin:8px 0;border-radius:16px;border:0;font-weight:900;font-style:italic;font-size:19px;letter-spacing:1px;cursor:pointer;box-shadow:0 5px 0 rgba(0,0,0,.35)}",
    ".bbov .p{background:#ff3d7f;color:#fff}.bbov .c{background:#3ec7ff;color:#1b1030}.bbov .s{background:rgba(255,255,255,.1);color:#fff;box-shadow:none}",
    ".bbov .btn:disabled{opacity:.35}",
    ".bbov input{width:100%;box-sizing:border-box;padding:14px;margin:8px 0 0;border-radius:14px;border:2px solid rgba(62,199,255,.55);background:#120a26;color:#fff;font-size:22px;font-weight:900;text-align:center;letter-spacing:4px;text-transform:uppercase;outline:none}",
    ".bbov .code{font-size:56px;font-weight:900;letter-spacing:10px;margin:4px 0;color:#3ec7ff;text-shadow:0 0 24px rgba(62,199,255,.5)}",
    ".bbov .spin{width:46px;height:46px;margin:16px auto;border-radius:50%;border:4px solid rgba(255,255,255,.15);border-top-color:#ff3d7f;animation:bbs .8s linear infinite}",
    "@keyframes bbs{to{transform:rotate(360deg)}}",
    ".bbov .err{color:#ff7a9a;min-height:18px;font-size:13px;margin-top:6px}",
    ".bbov ul{list-style:none;padding:0;margin:10px 0;text-align:left}",
    ".bbov li{display:flex;align-items:center;gap:10px;padding:10px;border-radius:12px;font-weight:700;background:rgba(255,255,255,.05);margin:6px 0}",
    ".bbov li.me{background:rgba(255,61,127,.22);outline:1px solid rgba(255,61,127,.6)}",
    ".bbov .rk{width:30px;text-align:center;font-weight:900;font-style:italic;opacity:.85}",
    ".bbov li:nth-child(1) .rk{color:#ffd23f}.bbov li:nth-child(2) .rk{color:#d9e2f2}.bbov li:nth-child(3) .rk{color:#ff9a5c}",
    ".bbov .nm{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}",
    ".bbov .nm small{display:block;opacity:.55;font-size:11px;letter-spacing:1px}",
    ".bbov .tm{font-weight:900;font-size:18px}",
    ".bbov .opt{display:flex;align-items:center;justify-content:space-between;gap:8px;margin:8px 0;background:rgba(255,255,255,.06);border-radius:14px;padding:8px 10px}",
    ".bbov .opt b{font-size:14px;opacity:.8;letter-spacing:1px}",
    ".bbov .opt button{background:rgba(255,255,255,.14);border:0;color:#fff;font-weight:900;border-radius:10px;padding:8px 12px;cursor:pointer;min-width:150px}",
    ".bbov .big{font-size:40px;font-weight:900;font-style:italic;color:#ffd23f;margin:6px 0}",
    ".bbov .empty{padding:26px;text-align:center;opacity:.6}"
  ].join("\n");
  document.head.appendChild(css);

  function overlay(id) {
    var o = document.createElement("div"); o.id = id; o.className = "bbov";
    o.innerHTML = '<div class="card"></div>';
    document.body.appendChild(o);
    ["keydown", "keyup", "keypress"].forEach(function (t) { o.addEventListener(t, function (e) { e.stopPropagation(); }, true); });
    return o;
  }

  // ---------------------------------------------------------------- leaderboard
  var lb = overlay("bblb");
  function showBoard() {
    lb.querySelector(".card").innerHTML = '<div class="hd"><h2>TOP BRAWLERS</h2><button class="x">&#10005;</button></div><div class="body"><p class="sub">Online wins. Every fighter in the match has to agree on who won.</p><ul><div class="empty">Loading...</div></ul></div>';
    lb.classList.add("on");
    lb.querySelector(".x").onclick = function () { lb.classList.remove("on"); };
    fetch(API + "/api/brawl/board?limit=30").then(function (r) { return r.json(); }).then(function (d) {
      var rows = d.top || [];
      lb.querySelector("ul").innerHTML = rows.length ? rows.map(function (r, i) {
        var me = meName && r.name === meName.toUpperCase();
        return '<li class="' + (me ? "me" : "") + '"><span class="rk">' + (i + 1) + '</span><span class="nm">' + esc(r.name) + (me ? " (YOU)" : "") + "<small>" + r.games + " FIGHTS</small></span><span class=\"tm\">" + r.wins + " W</span></li>";
      }).join("") : '<div class="empty">No online wins yet. Be the first champ!</div>';
    }).catch(function () { lb.querySelector("ul").innerHTML = '<div class="empty">Leaderboard offline. Try again soon.</div>'; });
    track("lb_open");
  }
  lb.addEventListener("click", function (e) { if (e.target === lb) lb.classList.remove("on"); });

  // ---------------------------------------------------------------- name prompt
  var nm = overlay("bbname");
  function askName(cb) {
    nm.querySelector(".card").innerHTML = '<div class="body" style="padding-top:22px"><h2>YOUR BRAWLER NAME</h2><p class="sub" style="margin-top:8px">Shown above your fighter and on the leaderboard</p>' +
      '<input maxlength="12" autocomplete="off" autocapitalize="characters" spellcheck="false" placeholder="NAME"><div class="err"></div>' +
      '<button class="btn p" data-a="ok">LET\'S GO</button><button class="btn s" data-a="skip">SKIP</button></div>';
    var input = nm.querySelector("input");
    input.value = meName || "";
    nm.classList.add("on");
    setTimeout(function () { input.focus(); }, 50);
    nm.querySelector('[data-a="ok"]').onclick = function () {
      var v = input.value.toUpperCase().replace(/[^A-Z0-9 _.-]/g, "").trim();
      if (v.length < 2) { nm.querySelector(".err").textContent = "At least 2 letters or numbers."; return; }
      meName = v; store("bb_name", v); nm.classList.remove("on"); cb();
    };
    nm.querySelector('[data-a="skip"]').onclick = function () { nm.classList.remove("on"); cb(); };
    input.onkeydown = function (e) { if (e.key === "Enter") nm.querySelector('[data-a="ok"]').click(); };
  }

  // ---------------------------------------------------------------- lobby
  var lob = overlay("bblobby");
  var card = lob.querySelector(".card");
  function view(html) {
    card.innerHTML = '<div class="hd"><h2>ONLINE BRAWL</h2><button class="x">&#10005;</button></div><div class="body">' + html + "</div>";
    lob.classList.add("on");
    card.querySelector(".x").onclick = function () { sendWs({ t: "leave" }); lob.classList.remove("on"); };
    bind();
  }
  function home(err) {
    view('<p class="sub">2 to 4 brawlers, rollback netcode, weapons from the sky. Last one standing wins.</p>' +
      '<button class="btn p" data-a="quick">QUICK MATCH</button>' +
      '<button class="btn c" data-a="create">CREATE A ROOM</button>' +
      '<input maxlength="4" placeholder="CODE" autocomplete="off" autocapitalize="characters" spellcheck="false">' +
      '<button class="btn s" data-a="join">JOIN WITH CODE</button><div class="err">' + (err || "") + "</div>");
  }
  function bind() {
    card.querySelectorAll("[data-a]").forEach(function (b) {
      b.onclick = function () {
        var a = b.getAttribute("data-a");
        if (a === "quick") connect(function () { sendWs({ t: "quick" }); view('<div class="spin"></div><p class="sub">Finding brawlers...<br>If nobody shows up soon you will fight CPUs.</p><button class="btn s" data-a="cancel">CANCEL</button>'); track("online_quick"); });
        if (a === "create") connect(function () { sendWs({ t: "create" }); track("online_create"); });
        if (a === "join") {
          var code = (card.querySelector("input").value || "").toUpperCase().replace(/[^A-Z]/g, "");
          if (code.length !== 4) { card.querySelector(".err").textContent = "Codes are 4 letters."; return; }
          joinCode(code);
        }
        if (a === "cancel") { sendWs({ t: "leave" }); home(); }
        if (a === "go") sendWs({ t: "go" });
        if (a === "invite") invite(b.getAttribute("data-code"));
        if (a === "stage") { cfg.stage = cfg.stage >= STAGES.length - 2 ? -1 : cfg.stage + 1; sendWs({ t: "config", stage: cfg.stage, bots: cfg.bots, botLv: cfg.botLv }); }
        if (a === "bots") { cfg.bots = (cfg.bots + 1) % 4; sendWs({ t: "config", stage: cfg.stage, bots: cfg.bots, botLv: cfg.botLv }); }
        if (a === "lv") { cfg.botLv = cfg.botLv >= 3 ? 1 : cfg.botLv + 1; sendWs({ t: "config", stage: cfg.stage, bots: cfg.bots, botLv: cfg.botLv }); }
      };
    });
  }
  function joinCode(code) { connect(function () { sendWs({ t: "join", code: code }); view('<div class="spin"></div><p class="sub">Joining ' + esc(code) + "...</p>"); track("online_join"); }); }
  function invite(code) {
    var url = location.origin + location.pathname + "?brawl=" + code;
    var text = "Come get BONKED! Join my BONK BRAWL room " + code;
    if (navigator.share) navigator.share({ title: "BONK BRAWL", text: text, url: url }).catch(function () {});
    else if (navigator.clipboard) navigator.clipboard.writeText(text + "\n" + url).then(function () { toast("Invite link copied!"); });
  }

  var countdownTimer = null;
  function showRoom(m) {
    clearInterval(countdownTimer);
    cfg.stage = m.stage; cfg.bots = m.bots; cfg.botLv = m.botLv;
    var rows = m.players.map(function (p, i) {
      return '<li class="' + (p.name === (meName || "").toUpperCase() ? "me" : "") + '"><span class="rk">P' + (i + 1) + '</span><span class="nm">' + esc(p.name) + "</span></li>";
    }).join("");
    for (var b = 0; b < m.bots && m.players.length + b < 4; b++) rows += '<li><span class="rk">P' + (m.players.length + b + 1) + '</span><span class="nm">CPU<small>' + LEVELS[m.botLv] + "</small></span></li>";
    var html = "";
    if (m.priv) html += '<p class="sub" style="margin:0">ROOM CODE</p><div class="code">' + m.code + '</div><button class="btn c" data-a="invite" data-code="' + m.code + '">INVITE FRIENDS</button>';
    html += '<ul>' + rows + "</ul>";
    if (m.priv && m.host) {
      html += '<div class="opt"><b>STAGE</b><button data-a="stage">' + STAGES[m.stage + 1] + '</button></div>' +
        '<div class="opt"><b>CPU FIGHTERS</b><button data-a="bots">' + m.bots + '</button></div>' +
        (m.bots > 0 ? '<div class="opt"><b>CPU LEVEL</b><button data-a="lv">' + LEVELS[m.botLv] + "</button></div>" : "");
      var ready = m.players.length + m.bots >= 2;
      html += '<button class="btn p" data-a="go"' + (ready ? "" : " disabled") + ">START FIGHT</button>" + (ready ? "" : '<p class="sub">Invite a friend or add a CPU.</p>');
    } else if (m.startsIn > 0) html += '<div class="big" id="bbcount">' + Math.ceil(m.startsIn / 1000) + '</div><p class="sub">Fight starts soon... more brawlers can still join</p>';
    else if (m.priv) html += '<div class="spin"></div><p class="sub">Waiting for the host to start...</p>';
    else html += '<div class="spin"></div><p class="sub">Waiting for more brawlers...</p>';
    html += '<button class="btn s" data-a="cancel">LEAVE</button>';
    view(html);
    if (m.startsIn > 0) {
      var end = Date.now() + m.startsIn;
      countdownTimer = setInterval(function () { var el = document.getElementById("bbcount"); if (el) el.textContent = Math.max(0, Math.ceil((end - Date.now()) / 1000)); }, 200);
    }
  }

  function connect(then) {
    if (ws && ws.readyState === 1) { sendWs({ t: "hello", name: meName || "BRAWLER", ch: myCh, player: playerId() }); then(); return; }
    if (ws) try { ws.close(); } catch (e) {}
    view('<div class="spin"></div><p class="sub">Connecting...</p>');
    ws = new WebSocket(WS_URL);
    var opened = false;
    ws.onopen = function () { opened = true; sendWs({ t: "hello", name: meName || "BRAWLER", ch: myCh, player: playerId() }); then(); };
    ws.onmessage = function (ev) {
      var d = ev.data;
      // hot path: rollback traffic goes straight to Unity without parsing here
      if (d.charCodeAt(7) === 34 && (d.charCodeAt(6) === 105 /* "i" */ || d.charCodeAt(6) === 104 /* "h" */)) { if (fighting) toUnityRaw(d); return; }
      var m; try { m = JSON.parse(d); } catch (e) { return; }
      switch (m.t) {
        case "lobby": if (!fighting) showRoom(m); break;
        case "error": home(m.msg); break;
        case "solo": clearInterval(countdownTimer); lob.classList.remove("on"); toUnity("OnNet", { t: "solo" }); break;
        case "start": clearInterval(countdownTimer); fighting = true; lob.classList.remove("on"); toUnityRaw(d); break;
        case "left": if (fighting) toUnityRaw(d); break;
        case "rank": toUnity("OnRank", { rank: m.rank, total: m.total, wins: m.wins }); break;
        case "rematchVote": toast("REMATCH " + m.n + "/" + m.of); break;
      }
    };
    ws.onclose = function () {
      clearInterval(countdownTimer);
      if (!opened) { home("Can't reach the brawl server. Try again in a moment."); return; }
      if (fighting) { fighting = false; toUnity("OnNet", { t: "end" }); }
      else if (lob.classList.contains("on")) home("Disconnected.");
      ws = null;
    };
  }

  function openLobby(ch) {
    if (ch !== undefined && ch !== null) myCh = ch;
    var go = function () { if (wantJoin) { var c = wantJoin; wantJoin = null; joinCode(c); } else home(); };
    if (meName) go(); else askName(go);
  }

  window.brawl = {
    ready: function () { if (wantJoin) setTimeout(function () { openLobby(null); }, 900); },
    board: showBoard,
    lobby: openLobby,
    send: function (json) { if (fighting && ws && ws.readyState === 1) ws.send(json); },
    leave: function () { fighting = false; sendWs({ t: "leave" }); }
  };
})();
