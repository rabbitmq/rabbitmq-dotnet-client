import Inline

info = {
    "friendly_name": "Line Break",
    "example_spacing": "",
    "example_template": "",
    "summary": "Inserts a line break in the middle of a paragraph.",
}

def SpanHandler(rest, acc):
    acc.append(Inline.TagFragment('pyle_br', []))
    return Inline.discardSpan(rest)
