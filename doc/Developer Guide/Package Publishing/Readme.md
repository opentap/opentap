Package Publishing
==================

## Plugin Publish Checklist
This is a list of point to check prior to published your package on [packages.opentap.io](https://packages.opentap.io).

### Update the Package Definition (`package.xml`) file:
* Provide a meaningful [`Name`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#attributes-in-the-configuration-file). We advise to make the names human readable, i.e. not Camel Case, no underscores, etc. No need to add "plugin" in the package name, as OpenTAP packages typically contain OpenTAP plugins. The name cannot contain '/' or be changed later, if you want to change the name you must publish the package again. 
* Provide [`<Description>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#description-element) of the package.
* Provide [`<Owner>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#owner-element). Should be the legal entity, this is likely to be the company you're working for or your name.
* If your project is open-source, provide [`<SourceUrl>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#sourceurl-element) with a link to your project (GitHub/GitLab etc.). You should also provide a [`<SourceLicense>`](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#sourcelicense-element) to signify the license your project is under. Finally, make sure your repository is publicly available.

See all the available package definition options [here](https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#packaging-configuration-file).

### Verify Dependencies
Make sure all dependencies are available on `packages.opentap.io`. Also, consider only depending on released versions.


## Publish the Package
There are two main ways of publishing a package.
- Login to [packages.opentap.io](https://packages.opentap.io), go to `Packages` and manually upload the package.
- Upload the package from a CLI, see [Uploading from CLI](#uploading-from-cli).

After upload, go to [packages.opentap.io](https://packages.opentap.io) and follow the steps below to make your package public. You only need to do this once, as this will affect all current and future uploaded versions of the package:
1. Login by clicking the `Login` button.
2. After login you will see a menu button with your profile name, click it and go to the `Admin` page. 
3. Navigate to `Packages`, find the package you want to publish and click the `Publish` button.
4. Fill out the form and click `Send Email Request` to send an email to the OpenTAP team.



### Uploading from CLI
To upload a package to [packages.opentap.io](https://packages.opentap.io) use these two commands:

```batch
tap package install "Repository Client" --version 1.0
tap repo upload MyPackage.TapPackage --token <USER_TOKEN>
```

> Note: You need a UserToken from the OpenTAP Repository before you can upload. On packages.opentap.io you can login and create new UserTokens directly on the homepage. For other OpenTAP repositories you should contact the administrator of that OpenTAP Repository.

Below is an example of a Github workflow that automatically publishes any TapPackages.


```yml
Publish:
  if: github.ref == 'refs/heads/main' || contains(github.ref, 'refs/heads/release') || contains(github.ref, 'refs/tags/v')
  environment: packages.opentap.io
  runs-on: ubuntu-latest
  needs:
    - Build
  steps:
    - name: Download Artifacts
      uses: actions/download-artifact@v3
      with:
        name: package
        path: ./
    - name: Setup OpenTAP
      uses: opentap/setup-opentap@v1.0
      with:
        version: 9.18.4
    - name: Install Repository Client
      run: tap package install -f "Repository Client" --version 1.0
    - name: Publish
      run: |
        tap repo upload -r https://packages.opentap.io -t ${{ secrets.USER_TOKEN }} -d /Packages/ *.TapPackage
```