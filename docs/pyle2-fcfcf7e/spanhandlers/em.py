import Inline

info = {
    "friendly_name": "Emphasised text",
    "summary": "Applies the 'emphasis' typestyle to the contained markup.",
}

def SpanHandler(rest, acc):
    (inner, rest) = Inline.parse(rest)
    acc.append(Inline.TagFragment('pyle_em', inner))
    return rest
