import Core
import web

info = {
    "friendly_name": "Backlinks",
    "example_spacing": "",
    "example_template": "",
    "summary": "Displays a list of pages that link to the current page.",
    "details": """

    <p>If an argument is supplied ("@backlinks SomePage"), it will
    display the backlinks for the named page instead of the current
    page.</p>

    """
}

class BackLinks(Core.Renderable):
    def __init__(self, pagename):
        self.pagename = pagename

    def prerender(self, format):
        self.backlinks = Core.backlinks(self.pagename, web.ctx.store.message_encoder())

    def templateName(self):
        return 'pyle_backlinks'

def SublanguageHandler(args, doc, renderer):
    pagename = args.strip()
    if not pagename:
        pagename = renderer.page.title
    renderer.add(BackLinks(pagename))
