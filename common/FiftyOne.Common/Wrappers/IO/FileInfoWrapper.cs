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
using System.IO;

namespace FiftyOne.Common.Wrappers.IO
{
    public class FileInfoWrapper : IFileInfoWrapper
    {
        public FileInfo InfoInstance { get; protected set; }
        protected Func<FileInfo, IFileInfoWrapper> FileInfoWrapperFactory { get; set; }
        protected Func<DirectoryInfo, IDirectoryInfoWrapper> DirectoryInfoWrapperFactory { get; set; }

        public FileInfoWrapper(
            Func<FileInfo, IFileInfoWrapper> fileInfoWrapperFactory,
            Func<DirectoryInfo, IDirectoryInfoWrapper> directoryInfoWrapperFactory,
            string filename)
        {
            FileInfoWrapperFactory = fileInfoWrapperFactory;
            DirectoryInfoWrapperFactory = directoryInfoWrapperFactory;
            InfoInstance = new FileInfo(filename);
        }
        public FileInfoWrapper(
            Func<FileInfo, IFileInfoWrapper> fileInfoWrapperFactory,
            Func<DirectoryInfo, IDirectoryInfoWrapper> directoryInfoWrapperFactory, 
            FileInfo fileinfo)
        {
            FileInfoWrapperFactory = fileInfoWrapperFactory;
            DirectoryInfoWrapperFactory = directoryInfoWrapperFactory;
            InfoInstance = fileinfo;
        }

        public string FullName
        {
            get { return InfoInstance.FullName; }
        }

        public DateTime LastWriteTimeUtc
        {
            get { return InfoInstance.LastWriteTimeUtc; }
        }

        public DateTime CreationTimeUtc
        {
            get { return InfoInstance.CreationTimeUtc; }
        }

        public DateTime LastAccessTimeUtc
        {
            get { return InfoInstance.LastAccessTimeUtc; }
        }

        public IDirectoryInfoWrapper Directory
        {
            get { return DirectoryInfoWrapperFactory(InfoInstance.Directory); }
        }

        public bool Exists
        {
            get { return InfoInstance.Exists; }
        }

        public FileStream Open(FileMode mode, FileAccess access, FileShare share)
        {
            return InfoInstance.Open(mode, access, share);
        }
        public StreamReader OpenText()
        {
            return InfoInstance.OpenText();
        }

        public void Delete()
        {
            InfoInstance.Delete();
        }

        public IFileInfoWrapper CopyTo(string destFileName, bool overwrite)
        {
            return FileInfoWrapperFactory(InfoInstance.CopyTo(destFileName, overwrite));
        }

        

    }
}
