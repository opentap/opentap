import { defineConfig } from 'vitepress'

export default defineConfig({
  title: "OpenTAP",
  description: "OpenTAP Documentation",
  markdown: {
    lineNumbers: true
  },
  outDir: '../public',
  themeConfig: {
    socialLinks: [
      { icon: 'github', link: 'https://github.com/opentap/opentap' }
    ],
    search: {
      provider: 'local'
    },
    editLink: {
      pattern: 'https://github.com/opentap/opentap/edit/main/doc/:path'
    },
    docFooter: {
      prev: false,
      next: false
    },
    logo: '/opentap.svg',
    siteTitle: false,

    nav: [
      { text: 'Homepage', link: 'https://opentap.io' }
    ],

    sidebar: [
      {
        text: "Getting Started",
        link: "/"
      },
      {
        text: 'User Guide',
        collapsed: false,
        items: [
          { text: 'Overview', link: 'User Guide/Introduction/Readme.md' },
          { text: 'CLI Usage', link: 'User Guide/CLI Usage/Readme.md' },
          { text: 'Editors', link: 'User Guide/Editors/Readme.md' }
        ]
      },
      {
        text: 'Developer Guide',
        collapsed: false,
        items: [
            { link: "Developer Guide/Introduction/Readme.md", text: "Introduction" },
            { link: "Developer Guide/What is OpenTAP/Readme.md", text: "OpenTAP Overview" },
            { link: "Developer Guide/Getting Started in Visual Studio/Readme.md", text: "Getting Started" },
            { link: "Developer Guide/Development Essentials/Readme.md", text: "Development Essentials" },
            { link: "Developer Guide/Test Step/Readme.md", text: "Test Step" },
            { link: "Developer Guide/Resources/Readme.md", text: "Resources" },
            { link: "Developer Guide/Result Listener/Readme.md", text: "Result Listener" },
            { link: "Developer Guide/Component Setting/Readme.md", text: "Component Setting" },
            { link: "Developer Guide/Annotations/Readme.md", text: "Annotations" },
            { link: "Developer Guide/Plugin Packaging and Versioning/Readme.md", text: "Plugin Packaging and Versioning" },
            { link: "Developer Guide/Package Publishing/Readme.md", text: "Package Publishing" },
            { link: "Developer Guide/Continuous Integration/Readme.md", text: "Continuous Integration (CI/CD)" },
            { link: "Developer Guide/Attributes/Readme.md", text: "Attributes" },
            { link: "Developer Guide/Known Issues/Readme.md", text: "Known Issues" },
            { link: "Developer Guide/Appendix/Readme.md", text: "Appendix: Macro Strings" }
        ]
      },
      {
        text: 'API Reference',
        link: '/api/index.html',
        target: '_blank'
      },
      {
        text: 'Release Notes',
        collapsed: true,
        items: [
          { link: '/Release Notes/ReleaseNote_OpenTAP9.28.md', text: "Release Notes - OpenTAP 9.28"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.27.md', text: "Release Notes - OpenTAP 9.27"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.26.md', text: "Release Notes - OpenTAP 9.26"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.25.md', text: "Release Notes - OpenTAP 9.25"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.24.md', text: "Release Notes - OpenTAP 9.24"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.23.md', text: "Release Notes - OpenTAP 9.23"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.22.md', text: "Release Notes - OpenTAP 9.22"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.21.md', text: "Release Notes - OpenTAP 9.21"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.20.md', text: "Release Notes - OpenTAP 9.20"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.19.md', text: "Release Notes - OpenTAP 9.19"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.18.md', text: "Release Notes - OpenTAP 9.18"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.17.md', text: "Release Notes - OpenTAP 9.17"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.16.md', text: "Release Notes - OpenTAP 9.16"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.15.md', text: "Release Notes - OpenTAP 9.15"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.14.md', text: "Release Notes - OpenTAP 9.14"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.13.md', text: "Release Notes - OpenTAP 9.13"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.12.md', text: "Release Notes - OpenTAP 9.12"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.11.md', text: "Release Notes - OpenTAP 9.11"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.10.md', text: "Release Notes - OpenTAP 9.10"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.9.md', text: "Release Notes - OpenTAP 9.9"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.8.md', text: "Release Notes - OpenTAP 9.8"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.7.md', text: "Release Notes - OpenTAP 9.7"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.6.md', text: "Release Notes - OpenTAP 9.6"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.5.md', text: "Release Notes - OpenTAP 9.5"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.4.md', text: "Release Notes - OpenTAP 9.4"},
          { link: '/Release Notes/ReleaseNote_OpenTAP9.3.md', text: "Release Notes - OpenTAP 9.3"}
        ]
      },
      {
        text: 'FAQ',
        link: 'FAQ/Readme.md'
      }
    ]
  }
})
