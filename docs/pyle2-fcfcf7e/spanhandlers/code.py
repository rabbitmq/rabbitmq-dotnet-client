import Inline

info = {
    "friendly_name": "Source Code (Inline)",
    "example_template": "codetext",
    "summary": "Prints the contained text in a source-code typeface, without interpreting it as markup.",
}

def SpanHandler(rest, acc):
    (inner, rest) = Inline.collectSpan(rest)
    acc.append(Inline.TagFragment('pyle_code', [Inline.LiteralFragment(inner)]))
    return rest
