function makeRequest() {
    try {
	return new XMLHttpRequest();
    } catch (ex) {
	try {
	    return new ActiveXObject("Microsoft.XMLHTTP");
	} catch (ey) {
	    try {
	        return new ActiveXObject("Msxml2.XMLHTTP");
	    } catch (ez) {
	        return null;
	    }
	}
    }
}

function resting() {
    return ((new Date()).getTime() - lastKeyUp) > 1000;
}

var SPACE = " ";
var CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_.!~*'()";
var HEX = "0123456789ABCDEF";

function hex(dec) {
    return HEX.charAt((dec >> 4) & 0xF) + HEX.charAt(dec & 0xF);
}

function urlEncode(text) {
    var result = "";
    for (var i = 0; i < text.length; i++ ) {
        var ch = text.charAt(i);
        if (ch == SPACE) {
            result += "+";
        } else if (CHARS.indexOf(ch) != -1) {
            result += ch;
        } else {
            result += "%" + hex(ch.charCodeAt(0));
        }
    }

    return result;
}

function refreshPreview() {
    var req = makeRequest();
    if (req == null) return;
    req.open('POST', previewUrl, false);
    req.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    req.send('body=' + urlEncode(document.getElementById('body').value));
    document.getElementById('previewholder').innerHTML = req.responseText;
}
