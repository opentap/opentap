Package Publishing
==================

## Plugin Publish Checklist
This is a list of point to check prior to published your package on [packages.opentap.io](https://packages.opentap.io).

### Update the Package Definition (`package.xml`) file:
* Provide [`<Description>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#description-element) of the package, the following formatting is supported:
  * `<Status>`: Use one of the following: `Concept` = work has not yet started; `Initial Development` = before 1st release; `Active Development` = 1st release has happened, work is being done on future release; `Maintenance` = no future release planned, but some defects will be fixed.
  * `<Contacts>`: such as (Developers/Planner/Manager) [Originator, Primary Owner, Secondary Owner] including email.
  * `<Prerequisites>`: if any. Specify what is required to be able to use this package.
  * `<Hardware>`: If relevant specify what is supported in terms of hardware/equipment, use product numbers and/or names.
  * `<Links>`: to direct users to relevant material, e.g. documentation.  
* Provide [`<Owner>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#owner-element). This is likely to be the company you're working for or your name.
* If your project is open-source, provide [`<SourceUrl>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#sourceurl-element) with a link to your project (GitHub/GitLab etc.). You should also provide a [`<SourceLicense>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#sourcelicense-element) to signify the license your project is under. Finally, make sure your repository is publicly available.

See all the available package definition options [here](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#packaging-configuration-file).


## Publish the Package
There are two main ways of publishing a package.
- Login to [packages.opentap.io](https://packages.opentap.io), go to `Packages` and manually upload the package.
- Upload the package from a CLI using the [Repository Client](https://github.com/opentap/repository-client). 

After upload, login to [packages.opentap.io](https://packages.opentap.io), go to `Packages`, click `Publish` and fill out the form. You only need to do this once, as this will affect all current and future uploaded versions of the package.