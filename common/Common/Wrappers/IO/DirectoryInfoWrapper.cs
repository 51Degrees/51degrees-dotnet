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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.Common.Wrappers.IO
{
    public class DirectoryInfoWrapper : IDirectoryInfoWrapper
    {
        public DirectoryInfo InfoInstance { get; protected set; }
        protected Func<FileInfo, IFileInfoWrapper> FileInfoFactory { get; set; }
        protected Func<DirectoryInfo, IDirectoryInfoWrapper> DirInfoFactory { get; set; }


        public DirectoryInfoWrapper(
            Func<FileInfo, IFileInfoWrapper> fileInfoFactory,
            Func<DirectoryInfo, IDirectoryInfoWrapper> dirInfoFactory,
            DirectoryInfo dirInfo)
        {
            FileInfoFactory = fileInfoFactory;
            DirInfoFactory = dirInfoFactory;
            InfoInstance = dirInfo;
        }
        public DirectoryInfoWrapper(
            Func<FileInfo, IFileInfoWrapper> fileInfoFactory,
            Func<DirectoryInfo, IDirectoryInfoWrapper> dirInfoFactory,
            string path)
        {
            FileInfoFactory = fileInfoFactory;
            DirInfoFactory = dirInfoFactory;
            InfoInstance = new DirectoryInfo(path);
        }

        public bool Exists
        {
            get { return InfoInstance.Exists; }
        }

        public string FullName
        {
            get { return InfoInstance.FullName; }
        }
        public string Name
        {
            get { return InfoInstance.Name; }
        }

        public IDirectoryInfoWrapper Parent
        {
            get { return DirInfoFactory(InfoInstance.Parent); }
        }

        public void Create()
        {
            InfoInstance.Create();
        }

        public IFileInfoWrapper[] GetFiles(string searchPattern, SearchOption searchOption)
        {
            return InfoInstance.GetFiles(searchPattern, searchOption)
                .Select(f => FileInfoFactory(f)).ToArray();
        }
        public IFileInfoWrapper[] GetFiles()
        {
            return InfoInstance.GetFiles()
                .Select(f => FileInfoFactory(f)).ToArray();
        }

        public IDirectoryInfoWrapper[] GetDirectories(string searchPattern, SearchOption searchOption)
        {
            return InfoInstance.GetDirectories(searchPattern, searchOption)
                .Select(d => DirInfoFactory(d)).ToArray(); ;
        }
        public IDirectoryInfoWrapper[] GetDirectories()
        {
            return InfoInstance.GetDirectories()
                .Select(d => DirInfoFactory(d)).ToArray(); ;
        }

        public IEnumerable<IDirectoryInfoWrapper> EnumerateParents()
        {
            if (InfoInstance.Parent != null)
            {
                var parentWrapper = DirInfoFactory(InfoInstance.Parent);
                yield return parentWrapper;
                foreach (var parent in parentWrapper.EnumerateParents())
                {
                    yield return parent;
                }
            }
        }

    }
}
