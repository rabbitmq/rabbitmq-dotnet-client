import Inline

info = {
    "friendly_name": "Comment (Inline)",
    "example_template": "text",
    "summary": "The text contained within the span is not rendered in the final displayed page.",
}

def SpanHandler(rest, acc):
    return Inline.discardSpan(rest)
