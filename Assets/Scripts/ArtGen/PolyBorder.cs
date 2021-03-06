﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Represents the edge of a province and creates list of points representing the shape of the edge.
/// </summary>
public class PolyBorder
{
    public Vector3 P1;
    public Vector3 P2;
    public Connection Connection;
    public List<Vector3> OrderedPoints;
    public List<Vector3> OrderedFinePoints;

    public Vector3 MidPoint
    {
        get
        {
            int num = OrderedPoints.Count;
            num = Mathf.RoundToInt(num * 0.5f);

            return OrderedPoints[num];
        }
    }

    public PolyBorder(Vector3 p1, Vector3 p2, Connection c, bool is_road = false)
    {
        P1 = p1;
        P2 = p2;
        Connection = c;
        OrderedPoints = new List<Vector3>();
        OrderedFinePoints = new List<Vector3>();

        if (is_road)
        {
            calc_points(true);
            apply_jitter(0.04f);
        }
        else
        {
            calc_points();
            apply_jitter(0.02f);
        }
    }

    public List<Vector3> GetFullLengthBorder()
    {
        List<Vector3> pts = new List<Vector3>();
        pts.Add(P1);
        pts.AddRange(OrderedPoints);
        pts.Add(P2);

        return pts;
    }

    public List<Vector3> GetFullLengthBorderMinusEnd(bool reversed = false)
    {
        List<Vector3> pts = new List<Vector3>();

        if (reversed)
        {
            PolyBorder pb = Reversed();
            pts.Add(pb.P1);
            pts.AddRange(pb.OrderedPoints);
        }
        else
        {
            pts.Add(P1);
            pts.AddRange(OrderedPoints);
        }
        
        return pts;
    }

    public PolyBorder Offset(Vector3 offset)
    {
        List<Vector3> ordered = new List<Vector3>();

        foreach (Vector3 v in OrderedPoints)
        {
            ordered.Add(v + offset);
        }

        PolyBorder pb = new PolyBorder(P1 + offset, P2 + offset, Connection);
        pb.OrderedPoints = ordered;

        return pb;
    }
    
    public PolyBorder Reversed()
    {
        List<Vector3> ordered = new List<Vector3>();
        ordered.AddRange(OrderedPoints);
        ordered.Reverse();

        List<Vector3> ordered_fine = new List<Vector3>();
        ordered_fine.AddRange(OrderedFinePoints);
        ordered_fine.Reverse();

        PolyBorder pb = new PolyBorder(P2, P1, Connection);
        pb.OrderedPoints = ordered;
        pb.OrderedFinePoints = ordered_fine;

        return pb;
    }

    void apply_jitter(float jitter)
    {
        float dist = 0.08f;
        List<Vector3> knots = new List<Vector3>();
        Vector3 norm = (P2 - P1).normalized;
        Vector3 prev = P1;

        foreach (Vector3 pt in OrderedPoints)
        {
            /*if (Vector3.Distance(prev, pt) < dist)
            {
                continue;
            }*/

            dist = UnityEngine.Random.Range(0.04f, 0.16f);

            Vector3 dir = (pt - prev).normalized;
            Vector3 lateral = Vector3.Cross(dir, Vector3.forward);
            Vector3 shift = pt + lateral * UnityEngine.Random.Range(-jitter, jitter);
            knots.Add(shift);

            prev = pt;
        }

        generate_ordered(knots, true);
    }

    void calc_points(bool is_road = false) // create several knots randomly shifted between p1 and p2
    {
        float dist = Vector3.Distance(P1, P2);
        float latdist = dist * 0.16f;
        int maxknots = Mathf.Max(Mathf.FloorToInt(dist / 0.30f), 2);

        if (is_road)
        {
            latdist = dist * 0.06f;
            maxknots++;
        }

        Vector3 dir = (P2 - P1).normalized;
        Vector3 lat = Vector3.Cross(dir, Vector3.forward);
        Vector3 midpt = (P1 + P2) / 2;
        List<Vector3> knotstarts = new List<Vector3>();

        List<Vector3> knots = new List<Vector3>();
        knots.Add(P1);
        knots.Add(P2);

        for (int j = 0; j < UnityEngine.Random.Range(1, maxknots); j++) // pick points on the line where knots will be
        {
            int limit = 0;
            Vector3 randpos = P1 + (dir * UnityEngine.Random.Range(dist * 0.15f, dist * 0.85f));

            while (knotstarts.Any(x => Vector3.Distance(randpos, x) < 0.15f) && limit < 25)
            {
                limit++;
                randpos = P1 + (dir * UnityEngine.Random.Range(dist * 0.15f, dist * 0.85f));
            }

            if (limit < 25)
            {
                knotstarts.Add(randpos);
            }
        }

        foreach (Vector3 v in knotstarts) // figure out the lateral shift of each knot
        {
            int limit = 0;
            float middist = Vector3.Distance(v, midpt);
            float scale = Mathf.Max(1f - (middist / (dist * 0.35f)), 0.30f);
            Vector3 finalpt = v + lat * UnityEngine.Random.Range(-latdist * scale, latdist * scale);

            while (Vector3.Distance(finalpt, v) < (latdist * scale * 0.10f) && limit < 10)
            {
                limit++;
                finalpt = v + lat * UnityEngine.Random.Range(-latdist * scale, latdist * scale);
            }

            knots.Add(finalpt);
        }

        knots = knots.OrderBy(x => dist_proj(x, P1, lat)).ToList();

        generate_ordered(knots);
    }

    void generate_ordered(List<Vector3> knots, bool generate_fine = false)
    {
        OrderedPoints = new List<Vector3>();
        CubicBezierPath path = new CubicBezierPath(knots.ToArray());

        float mindist = 0.06f;
        float spacing = 0.08f;
        float max = (float)(knots.Count - 1) - 0.12f;
        float i = 0.12f;
        Vector3 last = P1;

        while (i < max)
        {
            Vector3 pt = path.GetPoint(i);

            if (Vector3.Distance(pt, last) >= mindist)
            {
                OrderedPoints.Add(pt);
                last = pt;
            }

            i += spacing;
        }

        if (!generate_fine)
        {
            return;
        }

        OrderedFinePoints = new List<Vector3>();

        mindist = 0.04f;
        spacing = 0.03f;
        max = (float)(knots.Count - 1) - 0.04f;
        i = 0.04f;
        last = P1;

        while (i < max)
        {
            Vector3 pt = path.GetPoint(i);

            if (Vector3.Distance(pt, last) >= mindist)
            {
                OrderedFinePoints.Add(pt);
                last = pt;
            }

            i += spacing;
        }

        Vector3 mid_start = (P1 + OrderedFinePoints[0]) * 0.5f;
        Vector3 mid_end = (P2 + OrderedFinePoints[OrderedFinePoints.Count - 1]) * 0.5f;

        OrderedFinePoints.Insert(0, mid_start);
        OrderedFinePoints.Add(mid_end);
    }

    /*void calc_points_test() // test with static shapes.
    {
        Vector3 cur = P1;
        Vector3 dir = (P2 - P1).normalized;
        Vector3 lat = Vector3.Cross(dir, Vector3.forward);

        List<Vector3> vecs = new List<Vector3>();
        vecs.Add(P1 + (dir * 0.1f) + lat * .03f);
        vecs.Add(P2 - (dir * 0.1f) + lat * .03f);

        OrderedPoints = vecs;
    }

    void calc_points_shitty() // random jiggle. very shitty.
    {
        Vector3 cur = P1;
        Vector3 dir = (P2 - P1).normalized;
        Vector3 lat = Vector3.Cross(dir, Vector3.forward);

        List<Vector3> vecs = new List<Vector3>();

        while (Vector3.Distance(cur, P2) > 0.20f)
        {
            cur = cur + dir * 0.10f;

            vecs.Add(cur + lat * UnityEngine.Random.Range(-0.03f, 0.03f));
        }

        OrderedPoints = vecs;
    }

    void calc_points_v2() // bad
    {
        float dist = Vector3.Distance(P1, P2) * 0.3f;
        List<Vector3> vecs = new List<Vector3>();

        Vector3 midpt = (P1 + P2) / 2;
        Vector3 dir = (P2 - P1).normalized;
        Vector3 lat = Vector3.Cross(dir, Vector3.forward);
        Vector3 start = midpt + (dir * UnityEngine.Random.Range(dist * -1, dist)) + (lat * UnityEngine.Random.Range(-0.20f, 0.20f)); // start point can be closer to start or end
        Vector3 cur = start;
        Vector3 cur_proj = project(P1, start, lat);

        float spacing = 0.03f;
        float jiggle = 0.0f;
        float jigglemax = 0.05f;
        float jiggletarget = 0.0f;
        float jigglelerp = 0.012f;
        float lerptarget = 0.016f;
        float dist_p = Vector3.Distance(cur_proj, P2);
        int iterations = Mathf.Max(Mathf.FloorToInt(dist_p / spacing), 1);

        for (int i = 0; i < iterations; i++)
        {
            jigglelerp = Mathf.Lerp(jigglelerp, lerptarget, 0.001f);

            if (Mathf.Abs(jiggle - jiggletarget) < 0.001f)
            {
                jiggletarget = UnityEngine.Random.Range(-jigglemax, jigglemax);
                lerptarget = UnityEngine.Random.Range(0.012f, 0.016f);
            }
            else
            {
                jiggle = Mathf.Lerp(jiggle, jiggletarget, jigglelerp);
            }

            cur = cur + (dir * spacing) + (lat * jiggle);
            cur_proj = project(P1, cur, lat);

            float dist_up = Vector3.Distance(cur, cur_proj);
            float dist_end = Vector3.Distance(P2, cur_proj);

            if (dist_up > dist_end)
            {
                Vector3 fix1 = cur_proj + dist_end * lat;
                Vector3 fix2 = cur_proj - dist_end * lat;

                if (Vector3.Distance(fix1, cur) < Vector3.Distance(fix2, cur))
                {
                    cur = fix1;
                }
                else
                {
                    cur = fix2;
                }

                jigglelerp = 0.02f;
            }

            vecs.Add(cur);
        }

        cur = start;
        cur_proj = project(P1, start, lat);
        dist_p = Vector3.Distance(cur_proj, P1);
        iterations = Mathf.Max(Mathf.FloorToInt(dist_p / spacing), 1);
        jiggletarget = 0.0f;
        jigglelerp = 0.012f;

        for (int i = 0; i < iterations; i++)
        {
            jigglelerp = Mathf.Lerp(jigglelerp, lerptarget, 0.001f);

            if (Mathf.Abs(jiggle - jiggletarget) < 0.001f)
            {
                jiggletarget = UnityEngine.Random.Range(-jigglemax, jigglemax);
                lerptarget = UnityEngine.Random.Range(0.012f, 0.016f);
            }
            else
            {
                jiggle = Mathf.Lerp(jiggle, jiggletarget, jigglelerp);
            }

            cur = cur + (dir * -spacing) + (lat * jiggle);
            cur_proj = project(P1, cur, lat);

            float dist_up = Vector3.Distance(cur, cur_proj);
            float dist_end = Vector3.Distance(P1, cur_proj);

            if (dist_up > dist_end)
            {
                Vector3 fix1 = cur_proj + dist_end * lat;
                Vector3 fix2 = cur_proj - dist_end * lat;

                if (Vector3.Distance(fix1, cur) < Vector3.Distance(fix2, cur))
                {
                    cur = fix1;
                }
                else
                {
                    cur = fix2;
                }

                jigglelerp = 0.02f;
            }

            vecs.Add(cur);
        }

        OrderedPoints = vecs.OrderBy(x => dist_proj(x, P1, lat)).ToList();
    }*/

    float dist_proj(Vector3 pos, Vector3 anchor, Vector3 dir)
    {
        if (Vector3.Distance(pos, anchor) < 0.01f)
        {
            return Vector3.Distance(pos, anchor);
        }

        return Vector3.Distance(project(anchor, pos, dir), anchor);
    }

    Vector3 project(Vector3 target, Vector3 start, Vector3 dir)
    {
        Plane p = new Plane(dir, target);
        Ray r = new Ray(start, dir);
        float f = 0f;

        if (p.Raycast(r, out f))
        {
            Vector3 pt = r.GetPoint(f); // this is dumb. i hate unity

            return pt;
        }

        return start;
    }
}