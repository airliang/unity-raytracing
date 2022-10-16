/*
  This file implements derivate version of nth_element function in C#. Original Java source code is made by 
  Adam Horvath and you can find it from
  http://blog.teamleadnet.com/2012/07/quick-select-algorithm-find-kth-element.html
  
  This is free and unencumbered software released into the public domain.
*/
//https://github.com/mcraiha/CSharp-nth_element
// Nth_element made with Quick select algorithm. Custom comparer. nthToSeek is zero base index
using System;
using System.Collections.Generic;

public class std
{
	public delegate bool CompareFunc<T>(T a, T b);
	public static void nth_element<T>(T[] array, int startIndex, int nthToSeek, int endIndex, Comparison<T> comparison)
	{
		int from = startIndex;
		int to = endIndex;

		// if from == to we reached the kth element
		while (from < to)
		{
			int r = from, w = to;
			T mid = array[(r + w) / 2];

			// stop if the reader and writer meets
			while (r < w)
			{
				if (comparison(array[r], mid) > -1)
				{ // put the large values at the end
					T tmp = array[w];
					array[w] = array[r];
					array[r] = tmp;
					w--;
				}
				else
				{ // the value is smaller than the pivot, skip
					r++;
				}
			}

			// if we stepped up (r++) we need to step one down
			if (comparison(array[r], mid) > 0)
			{
				r--;
			}

			// the r pointer is on the end of the first k elements
			if (nthToSeek <= r)
			{
				to = r;
			}
			else
			{
				from = r + 1;
			}
		}

		return;
	}

	public static void nth_element<T>(ref List<T> array, int startIndex, int nthToSeek, int endIndex, CompareFunc<T> comparison)
	{
		int from = startIndex;
		int to = endIndex;

		// if from == to we reached the kth element
		while (from < to)
		{
			int r = from, w = to;
			T mid = array[(r + w) / 2];

			// stop if the reader and writer meets
			while (r < w)
			{
				//comparison false
				if (!comparison(array[r], mid))
				{ // put the large values at the end
					T tmp = array[w];
					array[w] = array[r];
					array[r] = tmp;
					w--;
				}
				else
				{ // the value is smaller than the pivot, skip
					r++;
				}
			}

			// if we stepped up (r++) we need to step one down
			if (comparison(array[r], mid))
			{
				r--;
			}

			// the r pointer is on the end of the first k elements
			if (nthToSeek <= r)
			{
				to = r;
			}
			else
			{
				from = r + 1;
			}
		}

		return;
	}

	// Nth_element made with Quick select algorithm. Default comparer. nthSmallest is zero base index
	public static void nth_element<T>(T[] array, int startIndex, int nthSmallest, int endIndex)
	{
		int from = startIndex;
		int to = endIndex;

		// if from == to we reached the kth element
		while (from < to)
		{
			int r = from, w = to;
			T mid = array[(r + w) / 2];

			// stop if the reader and writer meets
			while (r < w)
			{
				if (System.Collections.Generic.Comparer<T>.Default.Compare(array[r], mid) > -1)
				{ // put the large values at the end
					T tmp = array[w];
					array[w] = array[r];
					array[r] = tmp;
					w--;
				}
				else
				{ // the value is smaller than the pivot, skip
					r++;
				}
			}

			// if we stepped up (r++) we need to step one down
			if (System.Collections.Generic.Comparer<T>.Default.Compare(array[r], mid) > 0)
			{
				r--;
			}

			// the r pointer is on the end of the first k elements
			if (nthSmallest <= r)
			{
				to = r;
			}
			else
			{
				from = r + 1;
			}
		}

		return;
	}

	public delegate bool PartitionFunc<T>(T a);
	//end must not be search
	public static int partition<T>(ref List<T> list, int start, int end, PartitionFunc<T> p)
	{
		int first = start;

		while (true)
        {
			while (true)
            {
				if (first == end)
					return first;
				else if (p(list[first]))
					++first;
				else
					break;
            }
			--end;
			while (true)
            {
				if (first == end)
					return first;
				else if (!p(list[end]))
					--end;
				else
					break;
            }
			T tmp = list[end];
			list[end] = list[first];
			list[first] = tmp;
			++first;
        }

		return first;
	}
}

