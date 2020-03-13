# Helicon Actions

Actions are defined in the Helix file by using the respective custom tag. Below is the list of all actions supported by the current version of Helicon:

* [SetLogPath](#setlogpath)
* [SetLogEcho](#setlogecho)
* [Echo](#echo)
* [Trace](#trace)
* [DumpVars](#dumpvars)
* [Stop](#stop)
* [Skip](#skip)
* [Repeat](#repeat)
* [RaiseException](#raiseexception)
* [Shell](#shell)
* [SetVar](#setvar)
* [SetEnv](#setenv)
* [FileLoad](#fileload)
* [FileSave](#filesave)
* [FileAppend](#fileappend)
* [FileDelete](#filedelete)
* [FileCopy](#filecopy)
* [FileMove](#filemove)
* [SqlOpen](#sqlopen)
* [SqlClose](#sqlclose)
* [SqlStatement](#sqlstatement)
* [SqlLoadRow](#sqlloadrow)
* [SqlLoadArray](#sqlloadarray)
* [ForEachFile](#foreachfile)
* [ForEachRow](#foreachrow)
* [ForRange](#forrange)
* [If](#if)
* [SafeBlock](#safeblock)
* [Subroutine](#subroutine)
* [CallSubroutine](#callsubroutine)
* [Call](#call)
* [ApiCall](#apicall)
* [Pop3LoadArray](#pop3loadarray)
* [ImapLoadArray](#imaploadarray)
* [ImapOpen](#imapopen)
* [ImapClose](#imapclose)
* [ImapSetSeen](#imapsetseen)
* [ImapLoadMessage](#imaploadmessage)
* [LoadEmlMessage](#loademlmessage)
* [MsgLoadInfo](#msgloadinfo)
* [PdfLoadInfo](#pdfloadinfo)
* [PdfLoadTextArray](#pdfloadtextarray)
* [PdfMerge](#pdfmerge)
* [IFilterLoadText](#ifilterloadtext)
* [PdfLoadText](#pdfloadtext)
* [RegexExtract](#regexextract)
* [SplitText](#splittext)
* [ReplaceText](#replacetext)
* [Switch](#switch)
* [SendMail](#sendmail)
* [Sleep](#sleep)


<br/><br/><br/>
# SetLogPath
[Go Back](#helicon-actions)

Changes the path of the log file to the specified path. Default is the path where Helicon is located.

```xml
<SetLogPath>C:\LOGS</SetLogPath>
```


<br/><br/><br/>
# SetLogEcho
[Go Back](#helicon-actions)

Sets the log echo mode (innerText), when `TRUE` any log message will be also written to the console. Defaults to `TRUE`.

```xml
<SetLogEcho>False</SetLogEcho>
```


<br/><br/><br/>
# Echo
[Go Back](#helicon-actions)

Writes a message to the console (standard output).

```xml
<Echo>Hello World</Echo>
```


<br/><br/><br/>
# Trace
[Go Back](#helicon-actions)

Logs a message to the log file.

```xml
<Trace>Hello World</Trace>
```


<br/><br/><br/>
# DumpVars
[Go Back](#helicon-actions)

Writes the names of all variables in the context to standard output.

```xml
<DumpVars/>
```


<br/><br/><br/>
# Stop
[Go Back](#helicon-actions)

Stops the execution of the current block (or of the process if current execution is not inside a block). If executed from a subroutine it will cause to exit the subroutine and return to the caller.

```xml
<Stop/>
```


<br/><br/><br/>
# Skip
[Go Back](#helicon-actions)

Skips the current loop step and continues to the next, behaves similar to the `continue` keyword in C language. Should be used inside a loop. (i.e. [ForRange](#forrange))

```xml
<Skip/>
```


<br/><br/><br/>
# Repeat
[Go Back](#helicon-actions)

Breaks execution of the current loop step and restarts it from the top. Should be used inside a loop.

```xml
<Repeat/>
```


<br/><br/><br/>
# RaiseException
[Go Back](#helicon-actions)

Causes the system to raise an error exception with the specified message.

```xml
<RaiseException>Something bad happened!</RaiseException>
```


<br/><br/><br/>
# Shell
[Go Back](#helicon-actions)

Executes a shell command and introduces a variable named `Shell` with the output obtained from standard output of the command.

```xml
<Shell>echo Hello World</Shell>
```


<br/><br/><br/>
# SetVar
[Go Back](#helicon-actions)

Creates or modifies the value of a **context variable**. The `Name` attribute specifies the variable name. If attribute Eval is `TRUE` the value will be evaluated using the Helicon evaluator.

```xml
<SetVar Name="Pi">3.141592</SetVar>
<SetVar Name="Tau" Eval="True">(* 2 [[Pi]])</SetVar>
```


<br/><br/><br/>
# SetEnv
[Go Back](#helicon-actions)

Creates or modifies the value of an environment variable of the process. The `Name` attribute specifies the name of the variable. Note that the environment variable will be available to child processes, but not to the Helicon process nor any parent process.

```xml
<SetEnv Name="x">Hello</SetEnv>
<Shell>cmd /c "echo %x% World!"</Shell>
<Echo>[[Shell]]</Echo>
```


<br/><br/><br/>
# FileLoad
[Go Back](#helicon-actions)

Loads the contents of a file as a variable into the context. By default, if the file does not exist empty variables will be introduced. Use the `Strict` attribute to override this behavior and throw an exception if the file does not exist.

### Attributes
|Name						|Description
|-							|-
|Path 						|Specifies the path to the file. If the file does not exist no error will be thrown (unless `Strict` is set to `TRUE`), however empty variables will be introduced.
|Prefix						|Specifies a prefix to be prepended to all introduced variables. When set, variables will have the name “X.Y" where “X" is the prefix and “Y" is the variable name.
|Strict						|Boolean value (`TRUE`/`FALSE`) indicating if file existence should always be verified. When set to `TRUE` and the file does not exist an exception will be thrown. Defaults to `FALSE`.

### Introduced Variables
|Name 						|Description
|- 							|-
|FileData					|Contents of the file. Will be an empty string if the file doesn’t exist and `Strict` is `FALSE`.
|FileDataSize				|Size of FileData in bytes. Will be zero (0) if file does not exist and `Strict` is `FALSE`.

**NOTE:** The FileData value is provided as a `ByteArray`, to actually use it as a string use `[[STRING FileData]]`, or `[[HEXSTR FileData]]` if a hex string is desired.

### Example
```xml
<FileLoad Path="info.txt"/>
```


<br/><br/><br/>
# FileSave
[Go Back](#helicon-actions)

Saves the specified value to a file. If any error occurs an exception will be thrown.

### Attributes
|Name 						|Description
|- 							|-
|Path 						|Specifies the path of the destination file. If any sub-directory in this path does not exist it will be created.

### Example
```xml
<FileSave Path="test.txt">Hello World</FileSave>
```


<br/><br/><br/>
# FileAppend
[Go Back](#helicon-actions)

Appends the specified value to a file. If any error occurs an exception will be thrown.

### Attributes
|Name 						|Description
|- 							|-
|Path 						|Specifies the path of the destination file. If any sub-directory in this path does not exist it will be created.

### Example
```xml
<FileAppend Path="test.txt">Hello World</FileAppend>
```


<br/><br/><br/>
# FileDelete
[Go Back](#helicon-actions)

Deletes the specified file. If any error occurs an exception will be thrown.

### Attributes
|Name 						|Description
|- 							|-
|Path						|Specifies the path of the file to delete.

### Example
```xml
<FileDelete Path="test.txt"/>
```


<br/><br/><br/>
# FileCopy
[Go Back](#helicon-actions)

Copies a file to a target destination.

### Attributes
|Name 						|Description
|- 							|-
|Src 						|Source file path. If the file does not exist nothing will happen, no error will be reported.
|Dest 						|Specifies the path of the destination file. If the path ends with `\` it will be assumed that the destination file *name* is the same as the source. If any sub-directory of the path does not exist it will be created.

### Example
```xml
<FileCopy Src="test.txt" Dest="docs\"/>
```
```xml
<FileCopy Src="test.txt" Dest="docs\some-file.txt"/>
```


<br/><br/><br/>
# FileMove
[Go Back](#helicon-actions)

Copies a file to a target destination and removes the source file.

### Attributes
|Name 						|Description
|- 							|-
|Src 						|Source file path. If the file does not exist nothing will happen, no error will be reported.
|Dest 						|Specifies the path of the destination file. If the path ends with `\` it will be assumed that the destination file *name* is the same as the source. If any sub-directory of the path does not exist it will be created.

### Example
```xml
<FileMove Src="test.txt" Dest="docs\"/>
```
```xml
<FileMove Src="test.txt" Dest="docs\some-file.txt"/>
```


<br/><br/><br/>
# SqlOpen
[Go Back](#helicon-actions)



<br/><br/><br/>
# SqlClose
[Go Back](#helicon-actions)



<br/><br/><br/>
# SqlStatement
[Go Back](#helicon-actions)



<br/><br/><br/>
# SqlLoadRow
[Go Back](#helicon-actions)



<br/><br/><br/>
# SqlLoadArray
[Go Back](#helicon-actions)



<br/><br/><br/>
# ForEachFile
[Go Back](#helicon-actions)



<br/><br/><br/>
# ForEachRow
[Go Back](#helicon-actions)



<br/><br/><br/>
# ForRange
[Go Back](#helicon-actions)



<br/><br/><br/>
# If
[Go Back](#helicon-actions)



<br/><br/><br/>
# SafeBlock
[Go Back](#helicon-actions)



<br/><br/><br/>
# Subroutine
[Go Back](#helicon-actions)



<br/><br/><br/>
# CallSubroutine
[Go Back](#helicon-actions)



<br/><br/><br/>
# Call
[Go Back](#helicon-actions)



<br/><br/><br/>
# ApiCall
[Go Back](#helicon-actions)



<br/><br/><br/>
# Pop3LoadArray
[Go Back](#helicon-actions)



<br/><br/><br/>
# ImapLoadArray
[Go Back](#helicon-actions)



<br/><br/><br/>
# ImapOpen
[Go Back](#helicon-actions)



<br/><br/><br/>
# ImapClose
[Go Back](#helicon-actions)



<br/><br/><br/>
# ImapSetSeen
[Go Back](#helicon-actions)



<br/><br/><br/>
# ImapLoadMessage
[Go Back](#helicon-actions)



<br/><br/><br/>
# LoadEmlMessage
[Go Back](#helicon-actions)



<br/><br/><br/>
# MsgLoadInfo
[Go Back](#helicon-actions)



<br/><br/><br/>
# PdfLoadInfo
[Go Back](#helicon-actions)



<br/><br/><br/>
# PdfLoadTextArray
[Go Back](#helicon-actions)



<br/><br/><br/>
# PdfMerge
[Go Back](#helicon-actions)



<br/><br/><br/>
# IFilterLoadText
[Go Back](#helicon-actions)



<br/><br/><br/>
# PdfLoadText
[Go Back](#helicon-actions)



<br/><br/><br/>
# RegexExtract
[Go Back](#helicon-actions)



<br/><br/><br/>
# SplitText
[Go Back](#helicon-actions)



<br/><br/><br/>
# ReplaceText
[Go Back](#helicon-actions)



<br/><br/><br/>
# Switch
[Go Back](#helicon-actions)



<br/><br/><br/>
# SendMail
[Go Back](#helicon-actions)



<br/><br/><br/>
# Sleep
[Go Back](#helicon-actions)
