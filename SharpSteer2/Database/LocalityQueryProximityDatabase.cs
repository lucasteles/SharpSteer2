// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

/* ------------------------------------------------------------------ */
/*                                                                    */
/*                   Locality Query (LQ) Facility                     */
/*                                                                    */
/* ------------------------------------------------------------------ */
/*

	This utility is a spatial database which stores objects each of
	which is associated with a 3d point (a location in a 3d space).
	The points serve as the "search key" for the associated object.
	It is intended to efficiently answer "sphere inclusion" queries,
	also known as range queries: basically questions like:

		Which objects are within a radius R of the location L?

	In this context, "efficiently" means significantly faster than the
	naive, brute force O(n) testing of all known points.  Additionally
	it is assumed that the objects move along unpredictable paths, so
	that extensive preprocessing (for example, constructing a Delaunay
	triangulation of the point set) may not be practical.

	The implementation is a "bin lattice": a 3d rectangular array of
	brick-shaped (rectangular parallelepipeds) regions of space.  Each
	region is represented by a pointer to a (possibly empty) doubly-
	linked list of objects.  All of these sub-bricks are the same
	size.  All bricks are aligned with the global coordinate axes.

	Terminology used here: the region of space associated with a bin
	is called a sub-brick.  The collection of all sub-bricks is called
	the super-brick.  The super-brick should be specified to surround
	the region of space in which (almost) all the key-points will
	exist.  If key-points move outside the super-brick everything will
	continue to work, but without the speed advantage provided by the
	spatial subdivision.  For more details about how to specify the
	super-brick's position, size and subdivisions see lqCreateDatabase
	below.

	Overview of usage: an application using this facility would first
	create a database with lqCreateDatabase.  For each client object
	the application wants to put in the database it creates a
	lqClientProxy and initializes it with lqInitClientProxy.  When a
	client object moves, the application calls lqUpdateForNewLocation.
	To perform a query lqMapOverAllObjectsInLocality is passed an
	application-supplied call-back function to be applied to all
	client objects in the locality.  See lqCallBackFunction below for
	more detail.  The lqFindNearestNeighborWithinRadius function can
	be used to find a single nearest neighbor using the database.

	Note that "locality query" is also known as neighborhood query,
	neighborhood search, near neighbor search, and range query.  For
	additional information on this and related topics see:
	http://www.red3d.com/cwr/boids/ips.html

	For some description and illustrations of this database in use,
	see this paper: http://www.red3d.com/cwr/papers/2000/pip.html

*/

namespace SharpSteer2.Database;

/// <summary>
/// A AbstractProximityDatabase-style wrapper for the LQ bin lattice system
/// </summary>
public class LocalityQueryProximityDatabase<T> : IProximityDatabase<T> where T : class
{
    /// <summary>
    /// "token" to represent objects stored in the database
    /// </summary>
    sealed class TokenType(T parentObject, LocalityQueryProximityDatabase<T> lqsd)
        : ITokenForProximityDatabase<T>
    {
        LocalityQueryDatabase.ClientProxy proxy = new(parentObject);
        readonly LocalityQueryDatabase lq = lqsd.lq;

        public void Dispose()
        {
            if (proxy is null)
                return;

            lq.RemoveFromBin(proxy);
            proxy = null;
        }

        // the client obj calls this each time its position changes
        public void UpdateForNewPosition(Vector3 p) => lq.UpdateForNewLocation(proxy, p);

        // find all neighbors within the given sphere (as center and radius)
        public void FindNeighbors(Vector3 center, float radius, List<T> results) => lq.MapOverAllObjectsInLocality(center, radius, PerNeighborCallBackFunction, results);

        // called by LQ for each clientObject in the specified neighborhood:
        // push that clientObject onto the ContentType vector in void*
        // clientQueryState
        static void PerNeighborCallBackFunction(object clientObject, float distanceSquared, object clientQueryState)
        {
            List<T> results = (List<T>)clientQueryState;
            results.Add((T)clientObject);
        }
    }

    readonly LocalityQueryDatabase lq;

    // constructor
    public LocalityQueryProximityDatabase(Vector3 center, Vector3 dimensions, Vector3 divisions)
    {
        Vector3 halfsize = dimensions * 0.5f;
        Vector3 origin = center - halfsize;

        lq = new(origin, dimensions, (int)Math.Round(divisions.X), (int)Math.Round(divisions.Y), (int)Math.Round(divisions.Z));
    }

    // allocate a token to represent a given client obj in this database
    public ITokenForProximityDatabase<T> AllocateToken(T parentObject) => new TokenType(parentObject, this);

    // count the number of tokens currently in the database
    public int Count
    {
        get
        {
            int count = 0;
            lq.MapOverAllObjects((a, b, c) => count++, count);
            return count;
        }
    }
}
