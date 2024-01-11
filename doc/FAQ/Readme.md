# OpenTAP FAQ

## What is OpenTAP?
An open source test sequencing engine with a growing community of test developers dedicated to effortless test automation.  [Learn more](https://opentap.io/about).

## Who can use OpenTAP?
Anyone can contribute to, collaborate around and create test automation solutions with OpenTAP.  OpenTAP project code is available online at http://github.com/OpenTAP/OpenTAP 

## Who is responsible for maintaining OpenTAP?
OpenTAP is developed and maintained by a group of developers at Keysight [BD2]and other organizations.  You can view current project contributors at https://github.com/opentap/opentap/graphs/contributors.

## Are there restrictions on using, modifying or redistributing OpenTAP?
OpenTAP code is made available under the Mozilla Public License [MPL v.2](https://github.com/opentap/opentap/blob/main/LICENSE.txt). The terms of the MPL let users download, use, modify and redistribute project code as long as the code remains under the same open source MPL v.2 license.

## Where can I find the source code for OpenTAP?
OpenTAP project code is hosted at http://github.com/OpenTAP/OpenTAP.  You can also download ready-to-use packages on the OpenTAP [Downloads page](https://opentap.io/downloads).

## What is the OpenTAP Contributor License Agreement (CLA)?
A Contributor License Agreement CLA) ensures that even as third parties make contribution to the code base, the project organization retains full 
copyright to that code and accompanying artifacts. Under the OpenTAP CLA, users wishing to contribute to the OpenTAP engine, offering ideas, bug fixes, 
documentation, and any other code or content, must assign copyright to their contributions over to Keysight Technologies, the project founder. This 
assignment is performed via [OpenTAP CLA](https://opentap.io/assets/OpenTAP-CLA-v2.pdf). 

For OpenTAP plugins,  plugin creators/distributor define the terms and conditions under which those plugins are made available.  See the White Paper 
[Choosing a License for your OpenTAP Plugin](https://news.opentap.io/assets/whitepapers/Choosing-a-license-for-your-plugin-WP-v03.pdf) to learn about 
licensing and distribution options for your OpenTAP plugins.

## How can I obtain support for OpenTAP?
The OpenTAP developer community provides best effort support to users, developers and other ecosystem participants via the 
[OpenTAP issue tracker](https://github.com/opentap/opentap/issues) and on the [OpenTAP Forum](https://forum.opentap.io/). 
Commercial support is available for the Keysight commercial version of OpenTAP, 
[PathWave Test Automation](https://www.keysight.com/us/en/products/software/pathwave-test-software/pathwave-test-automation-software.html).

## On what OS platforms does OpenTAP run?
OpenTAP has been ported to and installs on both Windows and Linux hosts, as well as into Docker containers.  Learn more about OS support and installation options on the OpenTAP [Downloads page](https://opentap.io/downloads).

## What is an OpenTAP Plugin?
The OpenTAP engine communicates with devices, instruments and users via plugins.  Plugins serve a range of functions: interfaces to instruments and 
devices-under-test (DUTs), user interfaces, results listeners and test steps.  The OpenTAP project includes a large set of available plugins, especially 
interfaces for electronic test equipment. 

Learn more about OpenTAP [architecture](https://doc.opentap.io/Developer%20Guide/What%20is%20OpenTAP/#architecture) and 
[plugins](https://doc.opentap.io/Developer%20Guide/What%20is%20OpenTAP/#opentap-plugins) from the OpenTAP [Developer Guide](https://doc.opentap.io/Developer%20Guide/Introduction/).

## What tools are available to build new plugins?
The native programming language of OpenTAP and for OpenTAP plugins is C#.  You can also build OpenTAP plugins using the OpenTAP plugin for Python.  

You can control OpenTAP operations and compose test plans using the OpenTAP command line interface (CLI) and/or two available [Editors](https://doc.opentap.io/User%20Guide/Editors/).

## Can OpenTAP support a variety of test instruments with OpenTAP?
The OpenTAP project includes support for hundreds of test instruments from Keysight.  Non-Keysight test instruments can be utilized with OpenTAP in three ways:  

- using the [SCPI plugin](https://packages.opentap.io/index.html#/?name=%2Fpublic%2FScpiNetInstrumentBase&version=1.3.4%2B3cca403a&os=Windows&architecture=AnyCPU) and [OpenTap.IScpiInstrument API](https://doc.opentap.io/api/interface_open_tap_1_1_i_scpi_instrument.html) 
to interface to SCPI-enabled instruments

- obtain plugins from the OpenTAP ecosystem

- write your own plugin


## Can OpenTAP support other tools and programming languages?
The OpenTAP engine is implemented in C# and most OpenTAP plugins are also coded in that programming language, specifically with .NET.  There also exists a plugin and library (Python.NET) to support creation of plugins and other integrations in Python - learn more from 

- The Python [blog](https://blog.opentap.io/the-python-plugin) and [video](https://youtu.be/WxPn83N1HFQ) 

- OpenTAP Python [repository](https://github.com/opentap/OpenTap.Python) and [Documentation](https://doc.opentap.io/OpenTap.Python)

- [Download](https://packages.opentap.io/index.html#/?name=%2Fpublic%2FPython&version=2.4.1%2B6cf44290&os=Windows,Linux&architecture=AnyCPU) the plugin 

- View the Debugging C# and Python plugins [video](https://www.youtube.com/watch?v=FceQJk9WoNw)

## Can OpenTAP be used for applications and devices outside of Test and Measurement?
OpenTAP was conceived and first implemented to support controlling off-the-shelf test instruments and DUTs (Devices Under Test).  

But applications for OpenTAP are only limited by your imagination. OpenTAP can be configured to call a range of open APIs and drive 
almost any execution sequence, in the cloud, in your test lab and beyond.  OpenTAP has even been configured to support 
[Home Automation](https://blog.opentap.io/opentap-for-home-automation).

## What components and functions are installed default by the OpenTAP Installer?
A default OpenTAP installation includes the OpenTAP execution engine, Command Line Interface (CLI), the Plugin Development SDK and other 
software and documentation files.  You can find a full list of files included in an OpenTAP installation under the Files tab for the 
[OpenTAP Installation](http://packages.opentap.io/index.html#/?name=%2Fpublic%2FOpenTAP&version=9.19.0%2B22538968&os=Windows&architecture=x86).

## Where is OpenTAP being deployed today?
OpenTAP supports applications in education, R&D and manufacturing, enabling test automation in aerospace, automotive, medical, networking and wireless communications. Organizations large and small employ OpenTAP to streamline testing of products and services, in startups, across SMB and for enterprise organizations.
