/* 
 * Copyright (C) 2013 the QuickLibHost contributors. All rights reserved.
 * 
 * This file is part of QuickLibHost.
 * 
 * QuickLibHost is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * QuickLibHost is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.

 * You should have received a copy of the GNU Lesser General Public License
 * along with QuickLibHost.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;

namespace QuickLibHostClient
{
    public class QuickLibHostableAttribute : Attribute
    {
        public QuickLibHostableAttribute(string serviceName)
        {
            ServiceName = serviceName;
        }

        public string ServiceName { get; private set; }
    }
}
