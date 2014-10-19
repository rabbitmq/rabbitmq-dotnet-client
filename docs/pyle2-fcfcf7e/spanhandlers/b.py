import Inline

info = {
    "friendly_name": "Bold",
    "example_template": "boldtext",
    "summary": "Renders the contained markup with a bold typeface.",
}

def SpanHandler(rest, acc):
    (inner, rest) = Inline.parse(rest)
    acc.append(Inline.TagFragment('pyle_b', inner))
    return rest
