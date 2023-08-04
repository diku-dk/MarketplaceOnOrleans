using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Common.Entities
{
    /**
     * Package status list: https://dev.olist.com/docs/fulfillment
     * Fulfillment is a complex (and unfit) process to embrace in a benchmark
     * In this sense, we only use two of the statuses. Our adaptations are as follows:
     * Whenever packages are created, they are in shipped status.
     * Later on, the delivery transaction updates (some of) them to delivered.
     */
    public enum PackageStatus
    {
        created,
        ready_to_ship,
        canceled,
        shipped,
        lost,
        stolen,
        seized_for_inspection,
        returning_to_sender,
        returned_to_sender,
        awaiting_pickup_by_receiver,
        delivered
    }
}

