using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FtpUtil
{
    public interface IDateTimeProvider
    {
        DateTime GetCurrentDateTime();
    }
}
