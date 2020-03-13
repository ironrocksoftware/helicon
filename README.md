<p align="center">
  <img src="./docs/logo.png" alt="Helicon Logo"/>
</p>

# About Helicon

Helicon is an automation tool that runs **Helix Files** (more on that later) which describe a process. A process can be anything! Loading data from IMAP and storing on a database, monitoring a folder and sending files via E-Mail, loading data from a JSON/XML web service, etc.

## Installation

Unless you're planning to develop or modify the code of Helicon, you do not need to download the entire repository to use the tool. Downloading the [dist](./dist) folder is more than enough, we maintain that folder with the latest version binaries.

## Documentation

Want to start using it? Read the Helix Samples and the Documentation located in the [docs](./docs) folder, and have fun.

## Helix Files

Helicon operates by reading the contents of a file specified by the user. This file is known as a *Helix File* and contains one or more [actions](./docs/actions.md) to be executed by Helicon.

A Helix is an XML file with custom tags (defined by Helicon), more detailed information about all available tags can be found in the [docs](./docs) folder. Helix files must begin with a `<Process>` tag.

# Hello World

The following is a small hello world for Helicon. Save the following as "hello.hlx"

```xml
<Process>
	<Echo>Hello World</Echo>
</Process>
```

And run it by executing:

```sh
helicon hello.hlx
```

The output, as expected is simply a `Hello World` message. More examples can be found in the [docs/samples](./docs/samples) folder.
