import Inline

info = {
    "friendly_name": "HTML (Inline)",
    "example_template": "htmltext",
    "summary": "Inserts literal HTML into the rendered output.",
}

def SpanHandler(rest, acc):
    (inner, rest) = Inline.collectSpan(rest)
    acc.append(Inline.HtmlFragment(inner))
    return rest
