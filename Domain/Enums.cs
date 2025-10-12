using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain;

public enum MqttQueue
{
    CAN,
    General,
    GPS,
    Signal,
    Temperature,
    Voltage,
    Fingerprint,
    Person
}