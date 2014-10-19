import Core
import Plugin

info = {
    "friendly_name": "Plugin Details",
    "example_spacing": "",
    "example_template": "",
    "summary": "Inserts full descriptions of and help for each plugin installed.",
    "details": """

    <p>The text you are reading now was placed here by the plugindetails plugin.</p>

    """
}

class PluginDetails(Core.Renderable):
    def plugins(self):
        result = []
        for p in Plugin.all_plugins('spanhandlers'):
            result.append(Plugin.spanhandler_description(p))
        for p in Plugin.all_plugins('sublanguages'):
            result.append(Plugin.sublanguage_description(p))
        result.sort(lambda a, b: cmp(a['keyword'], b['keyword']))
        return result

    def templateName(self):
        return 'plugin_plugindetails'

def SublanguageHandler(args, doc, renderer):
    renderer.add(PluginDetails())
