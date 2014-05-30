using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FtpUtil
{
    public class myFTPFileInfo
    {
        public bool IsFolder { get; set; }
        public string Nombre { get; set; }
        public long Tamaño { get; set; }
        public DateTime Fecha { get; set; }
        public string Permisos { get; set; }
        public string Usuario { get; set; }
        public string Grupo { get; set; }
        private IDateTimeProvider _dateTimeProvider;

        public myFTPFileInfo(string ftpFileInfo) {
            _dateTimeProvider = new DefaultDateTimeProvider();
            Parse(ftpFileInfo);
        }

        public myFTPFileInfo(string ftpFileInfo, IDateTimeProvider dateTimeProvider) {
            if (dateTimeProvider != null) {
                _dateTimeProvider = dateTimeProvider;
            }
            else {
                _dateTimeProvider = new DefaultDateTimeProvider();
            }
            Parse(ftpFileInfo);
        }

        public bool Parse(string ftpFileInfo) {
            ////Eliminamos los caracteres de espacio repetitivos
            //while (ftpFileInfo.Contains("  ")) {
            //    ftpFileInfo=ftpFileInfo.replace("  "," ")
            //}
            var fileInfoData = ftpFileInfo.Split(new char[] { ' ' }, 9, StringSplitOptions.RemoveEmptyEntries);
            if (fileInfoData.Length == 9) {
                long tamañoArchivo;
                IsFolder = fileInfoData[0].StartsWith("d");
                Permisos = fileInfoData[0].Substring(1);
                Usuario = fileInfoData[2];
                Grupo = fileInfoData[3];
                long.TryParse(fileInfoData[4], out tamañoArchivo);
                Tamaño = tamañoArchivo;
                Fecha = ParseFTPDate(fileInfoData[5], fileInfoData[6], fileInfoData[7]);
                Nombre = fileInfoData[8];
                return true;
            }
            else {
                Permisos = "";
                Usuario = "";
                Grupo = "";
                Tamaño = 0;
                Fecha = DateTime.MinValue;
                Nombre = "";
                return false;
            }
        }

        public DateTime ParseFTPDate(string ftpMes, string ftpDia, string ftpAñoHora) {
            DateTime ret;
            int dia = 0, mes = 0, año = _dateTimeProvider.GetCurrentDateTime().Year;
            string hora = "00:00";

            switch (ftpMes.Trim().ToLower()) {
                case "jan":
                case "ene":
                    mes = 1;
                    break;
                case "feb":
                    mes = 2;
                    break;
                case "mar":
                    mes = 3;
                    break;
                case "apr":
                case "abr":
                    mes = 4;
                    break;
                case "may":
                    mes = 5;
                    break;
                case "jun":
                    mes = 6;
                    break;
                case "jul":
                    mes = 7;
                    break;
                case "aug":
                case "ago":
                    mes = 8;
                    break;
                case "sep":
                    mes = 9;
                    break;
                case "oct":
                    mes = 10;
                    break;
                case "nov":
                    mes = 11;
                    break;
                case "dec":
                case "dic":
                    mes = 12;
                    break;
            }

            int.TryParse(ftpDia, out dia);
            if (ftpAñoHora.Contains(":")) {
                DateTime fechaCalculada = DateTime.MinValue;
                hora = ftpAñoHora;
                
                //En el listado FTP aparecen sin año las fechas de los ultimos 365 dias.
                // Asi que la forma mas sencilla de lidiar con eso es que si la fecha calculada es mayor que la
                // fecha de hoy (considerando por default que al no especificar año se pone el año curso) entonces
                // significa que es fecha del año pasado
                DateTime.TryParse(string.Format("{0:0000}/{1:00}/{2:00} 00:00:00.000", año, mes, dia), out fechaCalculada);
                if (fechaCalculada > _dateTimeProvider.GetCurrentDateTime()) {
                    año -= 1;
                }
            }
            else {
                int.TryParse(ftpAñoHora, out año);
            }

            DateTime.TryParse(string.Format("{0:0000}/{1:00}/{2:00} {3}", año, mes, dia, hora), out ret);
            return ret;
        }

    }

}
