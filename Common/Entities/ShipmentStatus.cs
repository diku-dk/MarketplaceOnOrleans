using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Common.Entities
{
    /**
     * it seems there is no explicit shipment status,
     * only package status. So we derived from dev olist 
     * (https://dev.olist.com/docs/fulfillment) a possible
     * set of statuses for shipment:
     */
    public enum ShipmentStatus
    {
        // a shipment onbject is created as approved
        // originally approved only when packages are
        // created by sellers. but in the benchmark we
        // create them automatically as  part of business logic
        approved,
        // when at least one package is delivered
        delivery_in_progress,
        // when all packages are delivered
        concluded
    }
}

