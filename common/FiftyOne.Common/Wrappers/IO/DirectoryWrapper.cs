/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using System.Collections.Generic;
using System.IO;

namespace FiftyOne.Common.Wrappers.IO
{
    public class DirectoryWrapper : IDirectoryWrapper
    {
        public bool Exists(string path)
        {
            return Directory.Exists(path);
        }
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public string[] GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern);
            
        }

        public void Delete(string path, bool recursive = false)
        {
            Directory.Delete(path, recursive);
        }

        public IEnumerable<string> EnumerateDirectories(string path, 
            string searchPattern = "*", 
            SearchOption option = SearchOption.TopDirectoryOnly)
        {
            foreach (var dir in Directory.EnumerateDirectories(path, searchPattern, option))
            {
                yield return dir;
            }
        }

        public IEnumerable<string> EnumerateFiles(string path,
            string searchPattern = "*",
            SearchOption option = SearchOption.TopDirectoryOnly)
        {
            foreach (var file in Directory.EnumerateFiles(path, searchPattern, option))
            {
                yield return file;
            }
        }

        public string[] GetDirectories(string path,
            string searchPattern = "*",
            SearchOption option = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetDirectories(path, searchPattern, option);
        }

        public string[] GetFiles(string path,
            string searchPattern = "*",
            SearchOption option = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetFiles(path, searchPattern, option);
        }

        
    }
}
