This process describe how an OpenTAP community member can contribute an open source plugin to the OpenTAP group. We offer this option in an effort to have OpenTAP plugins collocated in one place making it easier for developers to find all available projects related to OpenTAP. 


The approach chosen in managing the plugin group is to ensure that all community members can contribute as [developers](https://docs.gitlab.com/ee/user/permissions.html#project-members-permissions) to all plugins hosted under Gitlab/OpenTAP/Plugins without forking. As a plugin owner you will be the [maintainer](https://docs.gitlab.com/ee/user/permissions.html#project-members-permissions) of your own created projects. 

> Note: Please include a license.md file that states your open source license in your project repository.

Steps to be taken by the developer to publishing the code on Gitlab: 
1)	Create a Gitlab account in case you do not have one yet
2)	Request access to the Gitlab/OpenTAP/Plugins group as a developer by sending a mail to plugins@opentap.io with your Gitlab user name.
3)	When granted access (you receive a mail from Gitlab), [Create](https://docs.gitlab.com/ee/gitlab-basics/create-project.html) the new project in the plugin [group](https://gitlab.com/OpenTAP/Plugins): https://gitlab.com/OpenTAP/Plugins (By clicking the + on the top bar, create new project). We recommend you to choose a name matching the OpenTAP package you will distribute in the group, see [examples](https://www.opentap.io/packages.html).
4)	Fill in the Blank project template.
    a.	Do not call your plugin: ‘plugin’ (You are already uploading code to the plugin group), provide a name that uniquely identify the capability of the plugin you provide, we recommend, you to name your plugin with the same name as the resulting OpenTAP package
    c.	Description should reflect what it enable, be aware of the character limits
    d.	Select public & create the project.
5)	Add code and push it upstream. More information on how to migrate your code can be found [here](https://docs.gitlab.com/ee/user/project/import/). 

If you have any questions or need help, feel free to contact us (plugins@opentap.io)
Congratulation you are done. Thank you from the OpenTAP team for your contribution.
