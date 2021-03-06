// iPhoneList - an iPhone File Explorer for Windows
//
// Copyright (C) 2007  James C. Baker
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// 
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace iPhoneGUI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            String dllPath = Environment.GetEnvironmentVariable("CommonProgramFiles");
            String IMDPath = dllPath + "\\Apple\\Mobile Device Support\\bin";
            Environment.SetEnvironmentVariable("PATH", IMDPath + ";" + Environment.GetEnvironmentVariable("PATH"));
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new iPhoneList());
        }
    }
}