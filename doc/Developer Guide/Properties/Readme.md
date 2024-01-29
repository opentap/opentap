Properties
===========

Most types of plugins has settings or some way of configuring them. 
Test Step, Instruments, Component Settings, User Inputs all have different properties that the user is sometimes required to configure.

In this chapter we will discuss what settings are, which kinds exists already and how to add support for new types of settings.

The way properties are presented to the user is through a graphical user interface (GUI).
In this text we assume that the KS8400 Editor GUI is being used but there is nothing blocking other editors from presenting the same things.

## What are Settings?

## Which kinds of properties are supported exists?

### Text / Strings 

### Numbers

The following types are considered numbers: double, float, int, long, short, ulong, uint, ushort.

These should be shown as a text box in UIs.

### SecureString

`SecureString` is a type for string data which should be handled in a secure fashion. That ensures that the string data is not stored unencrypted in memory. In OpenTAP using SecureString is also a signal the string should not be shown directly in the UI or saved in XML files. Hence when used in Editor it shows "dots" instead of the character written by the user.

Think carefully about when and how passwords are stored. When using SecureString to store a password in XML files, OpenTAP encrypts the string and saves it to the disk. The stored string can be decrypted with a key stored in OpenTAP, so this should not be viewed as a security feature as such only in the way that somebody who takes a quick glance at the XML file will not see a plaintext password.

If you want the user to type in the password every time the application is used, do the following to show the password prompt but not saving it anywhere:

```cs
[Browsable(false)]
[XmlIgnore]
public SecureString Password {get;set;} = new SecureString();
```

### Resources: Instruments, DUTs
