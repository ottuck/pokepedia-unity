mergeInto(LibraryManager.library, {
  // Unity -> 호스트. 템플릿이 정의한 허용 origin(window.pokeHost.allowedOrigins)에만 보낸다.
  // targetOrigin 이 실제 부모 origin 과 다르면 브라우저가 조용히 버리므로, 목록 전체에 보내도 한 곳에만 도착한다.
  HostBridge_Post: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    var msg;
    try { msg = JSON.parse(json); } catch (e) { console.error('[bridge] bad json', e); return; }
    if (!window.parent || window.parent === window) { console.log('[bridge] (no parent) ' + json); return; }
    var origins = (window.pokeHost && window.pokeHost.allowedOrigins) || [];
    for (var i = 0; i < origins.length; i++) {
      try { window.parent.postMessage(msg, origins[i]); } catch (e) { }
    }
  },

  // Unity -> 웹 템플릿의 시작 화면(HTML). 템플릿이 window.pokeTitle 을 정의한다(없으면 무시).
  HostBridge_ShowTitle: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    try { if (window.pokeTitle) window.pokeTitle.show(JSON.parse(json)); } catch (e) { console.error('[title] bad json', e); }
  },

  HostBridge_GetSearch: function () {
    var s = window.location.search || '';
    var len = lengthBytesUTF8(s) + 1;
    var buf = _malloc(len);
    stringToUTF8(s, buf, len);
    return buf;
  }
});
