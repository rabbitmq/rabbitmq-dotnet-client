using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    class RandomHostnameSelector : IHostnameSelector
    {
        string IHostnameSelector.NextFrom(IList<string> options)
        {
            return options.RandomItem();
        }
    }
}
