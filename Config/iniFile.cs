using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Config
{
    class INIFile
    {
        #region "声明变量"
        /// <summary>
        /// 写入INI文件
        /// </summary>
        /// <param name="section">节点名称[如[TypeName]]</param>
        /// <param name="key">键</param>
        /// <param name="val">值</param>
        /// <param name="filepath">文件路径</param>
        /// <returns></returns>
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filepath);
        /// <summary>
        /// 读取INI文件
        /// </summary>
        /// <param name="section">节点名称</param>
        /// <param name="key">键</param>
        /// <param name="def">值</param>
        /// <param name="retval">stringbulider对象</param>
        /// <param name="size">字节大小</param>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retval, int size, string filePath);
        #endregion

        public static void Writue(string sPath,string section, string key, string value)
        {
            // section=配置节，key=键名，value=键值，path=路径
            if (System.IO.File.Exists(sPath))
            {
                WritePrivateProfileString(section, key, value, sPath);
            }
        }

        public static string ReadValue(string sPath,string section, string key)
        {

            // 每次从ini中读取多少字节
            System.Text.StringBuilder temp = new System.Text.StringBuilder(255);
            // section=配置节，key=键名，temp=上面，path=路径
            if(System.IO.File.Exists(sPath))
            {
                GetPrivateProfileString(section, key, "", temp, 255, sPath);
            }
            return temp.ToString();


        }

    }
}
