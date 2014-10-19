import Inline

info = {
    "friendly_name": "Italics",
    "example_template": "italictext",
    "summary": "Displays the contained markup using an italic typeface.",
}

def SpanHandler(rest, acc):
    (inner, rest) = Inline.parse(rest)
    acc.append(Inline.TagFragment('pyle_i', inner))
    return rest
