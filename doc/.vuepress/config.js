module.exports = {
    title: "OpenTAP",
    description: 'OpenTAP is an Open Source project for fast and easy development and execution of automated tests.',
    dest: '../public',
    markdown: {
        lineNumbers: true
    },
    themeConfig: {
        repo: 'https://gitlab.com/OpenTAP/opentap',
        editLinks: true,
        editLinkText: 'Help improve this page!',
        lastUpdated: 'Last Updated',
        docsDir: 'doc',
        logo: '/OpenTAP.png',
        nav: [
            { text: 'Homepage', link: 'https://www.opentap.io' }
        ],
        sidebar: [
            "/",
            {
                title: 'Developer Guide',
                children: [
                    ["/Developer Guide/Introduction/", "Introduction"],
                    ["/Developer Guide/What is OpenTAP/", "What Is OpenTAP"],
                    {
                        title: 'Getting Started',
                        sidebarDepth: 0,
                        children: [
                            "/Developer Guide/Getting Started in Visual Studio/",
                            "/Developer Guide/Getting Started in Visual Studio/OpenTapNuget.md"
                        ]
                    },
                    ["/Developer Guide/Plugin Development/", "Plugin Development"],
                    ["/Developer Guide/Test Step/", "Test Step"],
                    ["/Developer Guide/DUT/", "DUT"],
                    ["/Developer Guide/Instrument Plugin Development/", "Instrument Plugin Development"],
                    ["/Developer Guide/Result Listener/", "Result Listener"],
                    ["/Developer Guide/Component Setting/", "Component Setting"],
                    ["/Developer Guide/Plugin Packaging and Versioning/", "Plugin Packaging and Versioning"],
                    ["/Developer Guide/Appendix A/", "Appendix A: Attribute Details"],
                    ["/Developer Guide/Appendix B/", "Appendix B: Macro Strings"]
                ]
            },
            {
                title: 'Release Notes',
                children: [
                    ['/Release Notes/ReleaseNote_OpenTAP9.3.md', "Release Notes - OpenTAP 9.3"],
                    ['/Release Notes/ReleaseNote_OpenTAP9.4.md', "Release Notes - OpenTAP 9.4"],
                    ['/Release Notes/ReleaseNote_OpenTAP9.5.md', "Release Notes - OpenTAP 9.5"]
                ]
            }
        ]
    }
}

