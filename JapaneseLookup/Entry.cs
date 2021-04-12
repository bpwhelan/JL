﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JapaneseLookup
{
    class Entry
    {
        public List<KEle> KEleList { get; set; }
        public List<REle> REleList { get; set; }
        public List<Sense> SenseList { get; set; }

        public Entry()
        {
            KEleList = new List<KEle>();
            REleList = new List<REle>();
            SenseList = new List<Sense>();
        }
    }
}