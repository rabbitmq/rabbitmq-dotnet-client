import Inline
import Core
import web

info = {
    "friendly_name": "Attachment",
    "example_template": "pagename:attachmentname",
    "summary": "Links to an attachment of this (or another, named) page.",
    "details": """

    <p>If invoked as [attachment some.filename], it will either embed (if
    the attachment is an image) or link to (otherwise) the named
    attachment. If invoked as [attachment PageName:some.filename], it
    will do the same, but for the named attachment on the named page
    instead of the current page.</p>

    """
}

def SpanHandler(rest, acc):
    (text, rest) = Inline.collectSpan(rest)

    parts = text.split('/', 1)
    name = parts[0]
    pagename = ''

    if name.find(':') != -1:
        (pagename, name) = name.split(':', 1)
    if not pagename:
        pagename = web.ctx.source_page_title

    if len(parts) > 1:
        alt = parts[1]
    else:
        alt = '[Attachment ' + pagename + ':' + name + ']'

    a = Core.Attachment(pagename, name, None)
    acc.append(AttachmentReference(a, alt))
    return rest

class AttachmentReference(Core.Renderable):
    def __init__(self, attachment, alt):
        self.attachment = attachment
        self.alt = alt

    def templateName(self):
        return 'pyle_attachmentreference'
