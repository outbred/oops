using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace URF.Base
{
    public class TrackableDictionary<TKey, TValue>
    {
        private Dictionary<TKey, TValue> _internal = new Dictionary<TKey, TValue>();
        public TrackableDictionary()
        {

        }
    }
}
