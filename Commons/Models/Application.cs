using Commons.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Commons.Models
{
    public class Application
    {
        private static int ApplicationCounter = 0;

        public int Id { get; set; }
        public ApplicationState State{ get; set; }
        public string CitizenId { get; set; }
        public string Name { get; set; }
        public DateTime DateOfBirth { get; set; }
        public Gender Gender { get; set; }

        public static int GetApplicationCounterNext()
        {
            return ApplicationCounter++;
        }
    }
}
