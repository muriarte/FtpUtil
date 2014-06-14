FtpUtil
=======

.Net Client FTP Library (C#)  
(There is another project called **muriarte/FtpUtilTest** which contains Unit and Integration testing for this project)

This project exposes a `myFTP` class providing methods to do the most common file and folder operations with an FTP Server.

##List of methods:  

- `List<string> GetFileList(string fileSpec)`  
  _Returns a list of filenames on the current FTP Server folder whose names meet the `fileSpec` parameter_  

- `List<myFTPFileInfo> GetFileListDetailed()`  
  _Returns a list of `myFTPFileInfo` objects for each file on the current FTP Server folder_  

- `List<myFTPFileInfo> GetFileListDetailed(string fileSpec)`  
  _Returns a list of `myFTPFileInfo` objects for each file on the current FTP Server folder  whose names meet the `fileSpec` parameter_  

- `List<myFTPFileInfo> GetFileListDetailed(string fileSpec, bool forceRefresh)`  
  _Returns a list of `myFTPFileInfo` objects for each file on the current FTP Server folder  whose names meet the `fileSpec` parameter forcing a refresh of the fileInfo cache is `forceRefresh` is `true`_  

- `bool ChangeFolder(string folder)`  
  _Change the current FTP Server folder. The folder specified can be absolute if it begins with '/' o relative to the current FTP Server folder if begins with any other character_  

- `string GetCurrentFolder()`  
  _Returns the path of current FTP Server folder_  

- `bool GetFile(string remoteFile, string localFile)`  
  _Download an FTP Server file and saves it on a local file_  

- `string GetFileString(string remoteFile)`  
  _Download an FTP Server file an returns its contents as a `string`_  

- `System.TimeSpan GetTimeDiff()`  
  _Tries to calculate the time difference between the local system and the FTP Server_  

- `long GetFileSize(string filename)`  
  _Returns the size of the specified file_  

- `bool DeleteFile(string filename)`  
  _Deletes a file on the FTP Server_  

- `bool DeleteFolder(string folderName)`  
  _Deletes a folder on the FTP Server_  

- `bool RenameFile(string filename, string newFilename)`  
  _Renames a file on the FTP Server_  

- `DateTime GetFileDateTime(string filename)`  
  _Returns the last changed time of a file on the FTP Server_  

- `bool UploadFileString(string remoteFilename, string contents)`  
  _Saves a file on the FTP Server with the text contents specified_  

- `bool UploadFile(string localFilename, string remoteFilename)`  
  _Uploads a file from the local system to the FTP Server_  

- `bool UploadFileBytes(string remoteFilename, byte[] fileContents)`  
  _Saves a file on the FTP Server with the binary contents specified_  

- `bool CreateFolder(string newFolderName)`  
  _Creates a folder on the FTP Server_  




##Methods (c#):  

---
###**`Class Constructor`**:  
`	myFTP(string server, string user, string password, string rootFolder)`

Parameters:  
`server`  - The FTP server address  
`user`  - The FTP login user name  
`password`  - The FTP login password  
`rootFolder`  - The root folder of the user on the FTP Server  

Example:  
`	var ftp = new myFTP("ftp://example.com", "username", "password", rootFolder);`
`	List<string> fileList;`  
`	fileList=ftp.GetFileList(null);`

---

