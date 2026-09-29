mergeInto(LibraryManager.library, {
  SD_Gameplay: function (on) { if (window.SD && window.SD.gameplay) window.SD.gameplay(!!on); },
  SD_Event: function (namePtr, value) { if (window.SD && window.SD.track) window.SD.track(UTF8ToString(namePtr), value); },
  SD_Ready: function () { if (window.SD && window.SD.ready) window.SD.ready(); },

  BB_Vibrate: function (ms) { try { if (navigator.vibrate && (!navigator.userActivation || navigator.userActivation.hasBeenActive)) navigator.vibrate(ms); } catch (e) {} },

  BB_ArmShare: function (textPtr) {
    var text = UTF8ToString(textPtr), w = window;
    var url = w.location.origin + w.location.pathname;
    var doShare = function () {
      if (!w.__bbPending) return;
      var t = w.__bbPending; w.__bbPending = null;
      if (navigator.share) navigator.share({ title: "BONK BRAWL", text: t, url: url }).catch(function () {});
      else if (navigator.clipboard) navigator.clipboard.writeText(t + "\n" + url).then(function () { w.bbToast && w.bbToast("Copied! Paste it anywhere"); });
      if (w.SD && w.SD.track) w.SD.track("share", 0);
    };
    if (!w.__bbHooked) { w.__bbHooked = true; ["pointerup", "touchend", "click"].forEach(function (ev) { w.addEventListener(ev, doShare, true); }); }
    w.__bbPending = text;
    setTimeout(doShare, 450);
  },

  BB_ShowBoard: function () { if (window.brawl) window.brawl.board(); },
  BB_NetOpen: function (ch) { if (window.brawl) window.brawl.lobby(ch); },
  BB_NetSend: function (jsonPtr) { if (window.brawl) window.brawl.send(UTF8ToString(jsonPtr)); },
  BB_NetLeave: function () { if (window.brawl) window.brawl.leave(); }
});
