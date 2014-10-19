import Inline

info = {
    "friendly_name": "Underline",
    "summary": "Displays the contained markup with underlining.",
}

def SpanHandler(rest, acc):
    (inner, rest) = Inline.parse(rest)
    acc.append(Inline.TagFragment('pyle_u', inner))
    return rest
