using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;

namespace FtpUtil
{
    public class myFTP
    {
        public string Server { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string RootFolder { get; set; }
        private string _workingFolder = "";
        private string _lastWorkingFolderListed = null;
        private bool _fileInfoListDirty = true;
        private bool getFileTimeStampNotSupported = false;
        private List<myFTPFileInfo> _fileInfoList = new List<myFTPFileInfo>();

        public myFTP(string server, string user, string password, string folder) {
            Server = server;
            User = user;
            Password = password;
            RootFolder = folder.Trim();
            if (RootFolder == null) {
                RootFolder = "";
            };
            if (RootFolder.Length > 0 && !RootFolder.StartsWith("/")) {
                RootFolder = "/" + RootFolder;
            };
            if (RootFolder.EndsWith("/")) {
                RootFolder = RootFolder.Substring(0, RootFolder.Length - 1);
            }
        }

        public List<string> GetFileList(string comodin) {
            if (comodin.Length > 0 && !comodin.StartsWith("/")) {
                comodin = "/" + comodin;
            }
            var ret = new List<string>();
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + comodin);
            req.Proxy = null;
            req.EnableSsl = false;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.ListDirectory;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                while (!reader.EndOfStream) {
                    ret.Add(reader.ReadLine());
                };
            };
            return ret;
        }

        public List<myFTPFileInfo> GetFileListDetailed() {
            return GetFileListDetailed("", false);
        }

        public List<myFTPFileInfo> GetFileListDetailed(string comodin) {
            return GetFileListDetailed(comodin, false);
        }

        public List<myFTPFileInfo> GetFileListDetailed(string comodin, bool forceRefresh) {
            if (comodin == null) {
                comodin = "";
            }
            if (comodin.Length > 0 && !comodin.StartsWith("/")) {
                comodin = "/" + comodin;
            }

            List<myFTPFileInfo> fileList = new List<myFTPFileInfo>();
            if (forceRefresh ||
                        comodin.Trim().Length > 0 ||
                        _fileInfoListDirty ||
                        _lastWorkingFolderListed != _workingFolder) {
                var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + comodin);
                req.Proxy = null;
                req.EnableSsl = false;
                req.Credentials = new NetworkCredential(User, Password);
                req.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                myFTPFileInfo ftpFileInfo;
                _fileInfoList.Clear();
                using (WebResponse resp = req.GetResponse()) {
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                        while (!reader.EndOfStream) {
                            ftpFileInfo = new myFTPFileInfo(reader.ReadLine());
                            if (ftpFileInfo.Nombre != null && ftpFileInfo.Nombre.Trim().Length > 0) {
                                fileList.Add(ftpFileInfo);
                            }
                        };
                    };
                };
                if (comodin.Trim().Length == 0) {
                    //Solo conserva cache de la lista de archivos cuando se solicita sin comodines
                    _fileInfoList = fileList;
                    _lastWorkingFolderListed = _workingFolder;
                    _fileInfoListDirty = false;
                }
                return fileList;
            }
            else {
                return _fileInfoList;
            }
        }

        public bool ChangeFolder(string folder) {
            _fileInfoListDirty = true;
            folder = folder.Trim();
            if (folder == null || folder.Length == 0 || folder == "/") {
                _workingFolder = "";
                return true;
            }
            if (folder.StartsWith("/")) {
                //Sustitución completa de folder relativo
                _workingFolder = folder.Substring(1);
            }
            else {
                //Append de folder relativo
                _workingFolder += "/" + folder;
            }
            if (_workingFolder.EndsWith("/")) {
                _workingFolder = _workingFolder.Substring(0, _workingFolder.Length - 1);
            }
            if (_workingFolder.Length > 0 && !_workingFolder.StartsWith("/")) {
                _workingFolder = "/" + _workingFolder;
            }

            return true;
        }

        public string GetCurrentFolder() {
            return RootFolder + _workingFolder;
        }

        public bool GetFile(string archivoRemoto, string archivoLocal) {
            string contenido = GetFileString(archivoRemoto);
            if (contenido != null) {
                File.WriteAllText(archivoLocal, contenido);
                return true;
            }
            return false;
        }

        public string GetFileString(string archivoRemoto) {
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + archivoRemoto);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.DownloadFile;

            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                return reader.ReadToEnd();
        }

        public System.TimeSpan GetTimeDiff() {
            string fileName = "integraasync.txt";
            DateTime current = DateTime.Now;
            DateTime fileDateTime = GetFileDateTime(fileName);
            //TimeSpan ret=TimeSpan.Zero;
            if (fileDateTime > DateTime.MinValue) {
                DeleteFile(fileName);
            }
            if (UploadFileString(fileName, "integraasync")) {
                fileDateTime = GetFileDateTime(fileName);
            }
            else {
                throw new ApplicationException("Problemas al subir el archivo para calculo de diferencia de tiempo con servidor FTP");
            }
            return current - fileDateTime;
        }

        public bool GetFileSize(string archivo) {
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + archivo);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.GetFileSize;

            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                Console.WriteLine(reader.ReadToEnd());
            return true;
        }

        public bool DeleteFile(string archivo) {
            _fileInfoListDirty = true;
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + archivo);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.DeleteFile;

            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                Console.WriteLine(reader.ReadToEnd());
            return true;
        }

        public bool RenameFile(string archivo, string nuevoNombre) {
            _fileInfoListDirty = true;
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + archivo);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.Rename;
            req.RenameTo = nuevoNombre;

            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                Console.WriteLine(reader.ReadToEnd());
            return true;
        }

        public DateTime GetFileDateTime(string archivo) {
            DateTime ret = DateTime.MinValue;
            if (getFileTimeStampNotSupported) {
                return (GetFileDateTimeByListingFiles(archivo));
            }

            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + archivo);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.GetDateTimestamp;

            try {
                using (WebResponse resp = req.GetResponse()) {
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                        try {
                            Console.WriteLine(resp.ContentType.ToString());
                        }
                        catch (System.NotImplementedException) {
                            //Al parecer el servidor no soporta el metodo para obtener la fecha de un archivo
                            // asi que usamos otro metodo
                            getFileTimeStampNotSupported = true;
                            return (GetFileDateTimeByListingFiles(archivo));
                        };
                        Console.WriteLine(reader.ReadToEnd());
                    }
                }
            }
            catch (System.Net.WebException ex) {
                FtpWebResponse ftpResponse = (FtpWebResponse)ex.Response;
                if (ftpResponse.StatusCode != FtpStatusCode.ActionNotTakenFileUnavailable) {
                    throw ex;
                }
            }
            return ret;
        }

        public bool UploadFileString(string remoteFilename, string contents) {
            byte[] fileContents = Encoding.UTF8.GetBytes(contents);
            return UploadFileBytes(remoteFilename, fileContents);

        }

        public bool UploadFile(string localFilename, string remoteFilename) {
            //Lee el contenido del archivo
            try {
                StreamReader sourceStream = new StreamReader(localFilename);
                byte[] fileContents = Encoding.UTF8.GetBytes(sourceStream.ReadToEnd());
                sourceStream.Close();
                return UploadFileBytes(remoteFilename, fileContents);
            }
            catch (Exception ex) {
                Console.WriteLine("Error en UploadFile:" + GetExceptionMessage(ex));
                return false;
            }
        }

        public bool UploadFileBytes(string remoteFilename, byte[] fileContents) {
            try {
                _fileInfoListDirty = true;
                var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + remoteFilename);
                req.Proxy = null;
                req.Credentials = new NetworkCredential(User, Password);
                req.Method = WebRequestMethods.Ftp.UploadFile;

                //Lee el contenido del archivo
                req.ContentLength = fileContents.Length;

                Stream requestStream = req.GetRequestStream();
                requestStream.Write(fileContents, 0, fileContents.Length);
                requestStream.Close();

                FtpWebResponse response = (FtpWebResponse)req.GetResponse();

                string responseStatus = response.StatusDescription;
                response.Close();

                return responseStatus.StartsWith("226 ");
            }
            catch (Exception ex) {
                Console.WriteLine("Error en UploadFileBytes:" + GetExceptionMessage(ex));
                return false;
            }
        }

        private DateTime GetFileDateTimeByListingFiles(string archivo) {
            DateTime ret = DateTime.MinValue;
            //Si es necesario actualiza la lista de archivos de la carpeta actual
            if (_workingFolder != _lastWorkingFolderListed || _fileInfoListDirty) {
                GetFileListDetailed(null);
            }
            foreach (myFTPFileInfo fileInfo in _fileInfoList) {
                if (fileInfo.Nombre.ToLower() == archivo.ToLower()) {
                    return fileInfo.Fecha;
                }
            }
            return ret;
        }

        private myFTPFileInfo GetFileInfo(string archivo) {
            myFTPFileInfo ret = null;
            //Si es necesario actualiza la lista de archivos de la carpeta actual
            if (_workingFolder != _lastWorkingFolderListed || _fileInfoListDirty) {
                GetFileListDetailed(null);
            }
            foreach (myFTPFileInfo fileInfo in _fileInfoList) {
                if (fileInfo.Nombre.ToLower() == archivo.ToLower()) {
                    return fileInfo;
                }
            }
            return ret;
        }

        private string GetExceptionMessage(Exception ex) {
            string ret = "";
            while (ex != null) {
                ret += "==>" + ex.Message + "\r\n";
                ex = ex.InnerException;
            }
            return ret;
        }
    }

}
