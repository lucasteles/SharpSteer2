// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

namespace SharpSteer2.Database;

/// <summary>
/// This structure represents the spatial database.  Typically one of
/// these would be created, by a call to lqCreateDatabase, for a given
/// application.
/// </summary>
class LocalityQueryDatabase
{
    // type for a pointer to a function used to map over client objects
    public delegate void LQCallBackFunction(object clientObject, float distanceSquared, object clientQueryState);

    /// <summary>
    /// This structure is a proxy for (and contains a pointer to) a client
    /// (application) obj in the spatial database.  One of these exists
    /// for each client obj.  This might be included within the
    /// structure of a client obj, or could be allocated separately.
    /// </summary>
    public class ClientProxy(object obj)
    {
        // previous obj in this bin, or null
        public ClientProxy Prev;

        // next obj in this bin, or null
        public ClientProxy Next;

        // bin ID (pointer-to-pointer to bin contents list)
        //public ClientProxy bin;
        // bin ID (index into bin contents list)
        public int? Bin;

        // pointer to client obj
        public readonly object Obj = obj;

        // the obj's location ("key point") used for spatial sorting
        public Vector3 Position;
    }

    // the origin is the super-brick corner minimum coordinates
    readonly Vector3 origin;

    // length of the edges of the super-brick
    readonly Vector3 size;

    // number of sub-brick divisions in each direction
    readonly int divX;
    readonly int divY;
    readonly int divZ;

    // pointer to an array of pointers, one for each bin
    // The last index is the extra bin for "everything else" (points outside super-brick)
    readonly ClientProxy[] bins;

    // extra bin for "everything else" (points outside super-brick)
    //ClientProxy other;

    /*
     * Allocate and initialize an LQ database, return a pointer to it.
     * The application needs to call this before using the LQ facility.
     * The nine parameters define the properties of the "super-brick":
     * (1) origin: coordinates of one corner of the super-brick, its
     *     minimum x, y and z extent.
     * (2) size: the width, height and depth of the super-brick.
     * (3) the number of subdivisions (sub-bricks) along each axis.
     * This routine also allocates the bin array, and initialize its
     * contents.
     */
    public LocalityQueryDatabase(Vector3 origin, Vector3 size, int divx, int divy, int divz)
    {
        this.origin = origin;
        this.size = size;
        divX = divx;
        divY = divy;
        divZ = divz;

        // The last index is the "other" bin
        int binCount = divx * divy * divz + 1;
        bins = new ClientProxy[binCount];
        for (int i = 0; i < bins.Length; i++)
        {
            bins[i] = null;
        }
    }

    /* Determine index into linear bin array given 3D bin indices */

    int BinCoordsToBinIndex(int ix, int iy, int iz) => ((ix * divY * divZ) + (iy * divZ) + iz);

    /* Call for each client obj every time its location changes.  For
       example, in an animation application, this would be called each
       frame for every moving obj.  */
    public void UpdateForNewLocation(ClientProxy obj, Vector3 position)
    {
        /* find bin for new location */
        int newBin = BinForLocation(position);

        /* store location in client obj, for future reference */
        obj.Position = position;

        /* has obj moved into a new bin? */
        if (newBin != obj.Bin)
        {
            RemoveFromBin(obj);
            AddToBin(obj, newBin);
        }
    }

    /* Adds a given client obj to a given bin, linking it into the bin
       contents list. */

    void AddToBin(ClientProxy obj, int binIndex)
    {
        /* if bin is currently empty */
        if (bins[binIndex] is null)
        {
            obj.Prev = null;
            obj.Next = null;
        }
        else
        {
            obj.Prev = null;
            obj.Next = bins[binIndex];
            bins[binIndex].Prev = obj;
        }

        bins[binIndex] = obj;

        /* record bin ID in proxy obj */
        obj.Bin = binIndex;
    }

    /* Find the bin ID for a location in space.  The location is given in
       terms of its XYZ coordinates.  The bin ID is a pointer to a pointer
       to the bin contents list.  */

    /*lqClientProxy*/ int BinForLocation(Vector3 position)
    {
        /* if point outside super-brick, return the "other" bin */
        if (position.X < origin.X || position.Y < origin.Y || position.Z < origin.Z ||
            position.X >= origin.X + size.X || position.Y >= origin.Y + size.Y || position.Z >= origin.Z + size.Z)
        {
            return bins.Length - 1;
        }

        /* if point inside super-brick, compute the bin coordinates */
        int ix = (int)(((position.X - origin.X) / size.X) * divX);
        int iy = (int)(((position.Y - origin.Y) / size.Y) * divY);
        int iz = (int)(((position.Z - origin.Z) / size.Z) * divZ);

        /* convert to linear bin number */
        int i = BinCoordsToBinIndex(ix, iy, iz);

        /* return pointer to that bin */
        return i; // (bins[i]);
    }

    /* Apply an application-specific function to all objects in a certain
       locality.  The locality is specified as a sphere with a given
       center and radius.  All objects whose location (key-point) is
       within this sphere are identified and the function is applied to
       them.  The application-supplied function takes three arguments:

         (1) a void* pointer to an lqClientProxy's "object".
         (2) the square of the distance from the center of the search
             locality sphere (x,y,z) to object's key-point.
         (3) a void* pointer to the caller-supplied "client query state"
             object -- typically NULL, but can be used to store state
             between calls to the lqCallBackFunction.

       This routine uses the LQ database to quickly reject any objects in
       bins which do not overlap with the sphere of interest.  Incremental
       calculation of index values is used to efficiently traverse the
       bins of interest. */
    public void MapOverAllObjectsInLocality(Vector3 center, float radius, LQCallBackFunction func,
        object clientQueryState)
    {
        int partlyOut = 0;
        bool completelyOutside =
            (((center.X + radius) < origin.X) ||
             ((center.Y + radius) < origin.Y) ||
             ((center.Z + radius) < origin.Z) ||
             ((center.X - radius) >= origin.X + size.X) ||
             ((center.Y - radius) >= origin.Y + size.Y) ||
             ((center.Z - radius) >= origin.Z + size.Z));

        /* is the sphere completely outside the "super brick"? */
        if (completelyOutside)
        {
            MapOverAllOutsideObjects(center, radius, func, clientQueryState);
            return;
        }

        /* compute min and max bin coordinates for each dimension */
        int minBinX = (int)((((center.X - radius) - origin.X) / size.X) * divX);
        int minBinY = (int)((((center.Y - radius) - origin.Y) / size.Y) * divY);
        int minBinZ = (int)((((center.Z - radius) - origin.Z) / size.Z) * divZ);
        int maxBinX = (int)((((center.X + radius) - origin.X) / size.X) * divX);
        int maxBinY = (int)((((center.Y + radius) - origin.Y) / size.Y) * divY);
        int maxBinZ = (int)((((center.Z + radius) - origin.Z) / size.Z) * divZ);

        /* clip bin coordinates */
        if (minBinX < 0)
        {
            partlyOut = 1;
            minBinX = 0;
        }

        if (minBinY < 0)
        {
            partlyOut = 1;
            minBinY = 0;
        }

        if (minBinZ < 0)
        {
            partlyOut = 1;
            minBinZ = 0;
        }

        if (maxBinX >= divX)
        {
            partlyOut = 1;
            maxBinX = divX - 1;
        }

        if (maxBinY >= divY)
        {
            partlyOut = 1;
            maxBinY = divY - 1;
        }

        if (maxBinZ >= divZ)
        {
            partlyOut = 1;
            maxBinZ = divZ - 1;
        }

        /* map function over outside objects if necessary (if clipped) */
        if (partlyOut != 0)
            MapOverAllOutsideObjects(center, radius, func, clientQueryState);

        /* map function over objects in bins */
        MapOverAllObjectsInLocalityClipped(
            center, radius,
            func,
            clientQueryState,
            minBinX, minBinY, minBinZ,
            maxBinX, maxBinY, maxBinZ);
    }

    /// <summary>
    /// Given a bin's list of client proxies, traverse the list and invoke
    /// the given lqCallBackFunction on each obj that falls within the
    /// search radius.
    /// </summary>
    /// <param name="co"></param>
    /// <param name="radiusSquared"></param>
    /// <param name="func"></param>
    /// <param name="state"></param>
    /// <param name="position"></param>
    static void TraverseBinClientObjectList(ClientProxy co, float radiusSquared, LQCallBackFunction func,
        object state, Vector3 position)
    {
        while (co != null)
        {
            // compute distance (squared) from this client obj to given
            // locality sphere's centerpoint
            Vector3 d = position - co.Position;
            float distanceSquared = d.LengthSquared();

            // apply function if client obj within sphere
            if (distanceSquared < radiusSquared)
                func(co.Obj, distanceSquared, state);

            // consider next client obj in bin list
            co = co.Next;
        }
    }

    /// <summary>
    /// This subroutine of lqMapOverAllObjectsInLocality efficiently
    /// traverses of subset of bins specified by max and min bin
    /// coordinates.
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    /// <param name="func"></param>
    /// <param name="clientQueryState"></param>
    /// <param name="minBinX"></param>
    /// <param name="minBinY"></param>
    /// <param name="minBinZ"></param>
    /// <param name="maxBinX"></param>
    /// <param name="maxBinY"></param>
    /// <param name="maxBinZ"></param>
    void MapOverAllObjectsInLocalityClipped(Vector3 center, float radius,
        LQCallBackFunction func,
        object clientQueryState,
        int minBinX, int minBinY, int minBinZ,
        int maxBinX, int maxBinY, int maxBinZ)
    {
        int i;
        int slab = divY * divZ;
        int row = divZ;
        int istart = minBinX * slab;
        int jstart = minBinY * row;
        int kstart = minBinZ;
        float radiusSquared = radius * radius;

        /* loop for x bins across diameter of sphere */
        int iindex = istart;
        for (i = minBinX; i <= maxBinX; i++)
        {
            /* loop for y bins across diameter of sphere */
            int jindex = jstart;
            int j;
            for (j = minBinY; j <= maxBinY; j++)
            {
                /* loop for z bins across diameter of sphere */
                int kindex = kstart;
                int k;
                for (k = minBinZ; k <= maxBinZ; k++)
                {
                    /* get current bin's client obj list */
                    ClientProxy bin = bins[iindex + jindex + kindex];
                    ClientProxy co = bin;

                    /* traverse current bin's client obj list */
                    TraverseBinClientObjectList(co,
                        radiusSquared,
                        func,
                        clientQueryState,
                        center);
                    kindex += 1;
                }

                jindex += row;
            }

            iindex += slab;
        }
    }

    /// <summary>
    /// If the query region (sphere) extends outside of the "super-brick"
    /// we need to check for objects in the catch-all "other" bin which
    /// holds any object which are not inside the regular sub-bricks
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    /// <param name="func"></param>
    /// <param name="clientQueryState"></param>
    void MapOverAllOutsideObjects(Vector3 center, float radius, LQCallBackFunction func,
        object clientQueryState)
    {
        ClientProxy co = bins[bins.Length - 1];
        float radiusSquared = radius * radius;

        // traverse the "other" bin's client object list
        TraverseBinClientObjectList(co, radiusSquared, func, clientQueryState, center);
    }

    static void MapOverAllObjectsInBin(ClientProxy binProxyList, LQCallBackFunction func,
        object clientQueryState)
    {
        // walk down proxy list, applying call-back function to each one
        while (binProxyList != null)
        {
            func(binProxyList.Obj, 0, clientQueryState);
            binProxyList = binProxyList.Next;
        }
    }

    /* Apply a user-supplied function to all objects in the database,
       regardless of locality (cf lqMapOverAllObjectsInLocality) */
    public void MapOverAllObjects(LQCallBackFunction func, object clientQueryState)
    {
        foreach (ClientProxy bin in bins)
        {
            MapOverAllObjectsInBin(bin, func, clientQueryState);
        }
    }

    /* Removes a given client obj from its current bin, unlinking it
       from the bin contents list. */
    public void RemoveFromBin(ClientProxy obj)
    {
        /* adjust pointers if obj is currently in a bin */
        if (obj.Bin != null)
        {
            /* If this obj is at the head of the list, move the bin
               pointer to the next item in the list (might be null). */
            if (bins[obj.Bin.Value] == obj)
                bins[obj.Bin.Value] = obj.Next;

            /* If there is a prev obj, link its "next" pointer to the
               obj after this one. */
            if (obj.Prev != null)
                obj.Prev.Next = obj.Next;

            /* If there is a next obj, link its "prev" pointer to the
               obj before this one. */
            if (obj.Next != null)
                obj.Next.Prev = obj.Prev;
        }

        /* Null out prev, next and bin pointers of this obj. */
        obj.Prev = null;
        obj.Next = null;
        obj.Bin = null;
    }
}
