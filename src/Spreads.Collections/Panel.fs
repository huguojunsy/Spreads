﻿namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Spreads
open Spreads.Collections


[<AbstractClassAttribute>]
[<AllowNullLiteral>]
[<SerializableAttribute>]
type Panel<'TRowKey,'TColumnKey, 'TValue>() =
  inherit Series<'TRowKey, Series<'TColumnKey, 'TValue>>()

  abstract Columns : Series<'TColumnKey, Series<'TRowKey,'TValue>> with get
  abstract Rows : Series<'TRowKey, Series<'TColumnKey,'TValue>> with get

//  /// Map rows to new rows as Series
//  abstract RowWise<'TResult> : mapper:Func<Series<'TColumnKey,'TValue>,Series<'TColumnKey,'TResult>> -> Panel<'TRowKey,'TColumnKey, 'TResult>
//  /// Reduce rows to a value
//  abstract RowWise<'TResult> : reducer:Func<Series<'TColumnKey,'TValue>,'TResult> -> Series<'TRowKey,'TResult>
//
//  /// Map columns to new columns
//  abstract ColumnWise<'TResult> : mapper:Func<Series<'TRowKey,'TValue>,Series<'TRowKey,'TResult>> -> Panel<'TRowKey,'TColumnKey, 'TResult>
//  /// Reduce columns to a value
//  abstract ColumnWise<'TResult> : reducer:Func<Series<'TRowKey,'TValue>,'TResult> -> Series<'TColumnKey,'TResult>




// ColumnsPanel uses ZipN cursor over source columns
// It is an IOrderedMap of IReadOnlyOrderedMap as columns, therefore we could add/remove columns

// RowsPanel has metarialized rows
// It is an IOrderedMap of arrays as rows + column key arrays

// RowsSeriesPanel or Surphace has rows as series, e.g. any of the two above mapped to some continuous series
// YieldCurve => Splines is on-demand 


type ColumnPanel<'TRowKey,'TColumnKey, 'TValue> (columns:Series<'TColumnKey, Series<'TRowKey,'TValue>>) =
  inherit Panel<'TRowKey,'TColumnKey, 'TValue>()
  let columnKeys = columns.Keys |> Seq.toArray 

  // NB :) this is the only required method to get a functioning panel
  // Note from signatures that if we add Panel<K,C,V> to Series<K,V>, then: 
  // (Panel as Series<K, [inner series]>) + Series<K,V>) => for each K, we will have (inner Series<C,V> + V)
  // which is already supported by series.
  // The same is true for two panels, which will end up for each key as (inner Series<C,V> + other inner Series<C,V>)


  override this.GetCursor() : ICursor<'TRowKey, Series<'TColumnKey, 'TValue>> = 
    new ZipNCursor<'TRowKey,'TValue,Series<'TColumnKey, 'TValue>>(
      Func<'TRowKey,'TValue[],Series<'TColumnKey, 'TValue>>(fun k vArr -> 
        // vArr is the buffer from ZipNCursor, we must copy it to a new array (from a pool) to return a series
        // if we want to avoid copying, we could alway use ZipN on this.Columns
        let vArrCopy = vArr |> Array.copy
        if columns.IsIndexed then
          let res = IndexedMap.OfSortedKeysAndValues(columnKeys, vArrCopy) :> Series<'TColumnKey, 'TValue>
          // TODO IsMutable <- false
          res
        else
          let res = SortedMap.OfSortedKeysAndValues(columnKeys, vArrCopy) :> Series<'TColumnKey, 'TValue>
          // TODO IsMutable <- false
          res
      ),
      columns.Values |> Seq.map (fun s -> s.GetCursor) |> Seq.toArray)
    :> ICursor<'TRowKey, Series<'TColumnKey, 'TValue>>

  // By being Series<>, Panel already implements IROOM for Rows

  override this.Columns 
    with get() : Series<'TColumnKey, Series<'TRowKey,'TValue>> = 
      // NB TODO bad thing, we are assuming that IOrderedMap is alway Series
      // Need an extension method? Or documen this. Or create a type that inherit from series and then is used as parent for Sorted/Indexed maps
      columns :?> Series<'TColumnKey, Series<'TRowKey,'TValue>>
  override this.Rows with get() = this :> Series<'TRowKey, Series<'TColumnKey,'TValue>>

//  override this.RowWise<'TResult>(mapper:Func<Series<'TColumnKey,'TValue>,Series<'TColumnKey,'TResult>>) : Panel<'TRowKey,'TColumnKey,'TResult> =
//    let zipCursor() = 
//      if columns.IsIndexed then
//        // TODO IndexedMap => OrderedMap with IsIndexed as a property
//        // SortedMap is optimized for regular keys, but in case of panels we reuse keys
//        let im = OrderedMap()
//        im.IsMutable <- false
//        im.keys <- columnKeys
//        im.size <- columnKeys.Length
//        let mutable newColumnKeys : 'TColumnKey[] = Unchecked.defaultof<_>
//        new ZipNCursor<'TRowKey,'TValue,'TResult[]>(
//          Func<'TRowKey,'TValue[],'TResult[]>(fun k vArr -> 
//            let vArrCopy = vArr |> Array.copy
//            im.values <- vArrCopy 
//            let newMap = mapper.Invoke(im)
//            let newArray = 
//              // TODO check if we have a sorted map as a result of mapper.Invoke
//              // and use its values directly
//              newMap.Values |> Seq.toArray
//            if Unchecked.equals newColumnKeys Unchecked.defaultof<'TColumnKey[]> then
//              newColumnKeys <- newMap.Keys |> Seq.toArray
//            else
//              if columnKeys.Length <> newArray.Length then invalidOp "RowWise series mappers returns series of different size"
//            newArray
//          ),
//          columns.Values |> Seq.map (fun s -> s.GetCursor) |> Seq.toArray)
//        :> ICursor<'TRowKey, 'TResult[]>
//      else
//        let sm = SortedMap()
//        sm.IsMutable <- false
//        sm.keys <- columnKeys
//        sm.size <- columnKeys.Length
//        let mutable newColumnKeys : 'TColumnKey[] = Unchecked.defaultof<_>
//        new ZipNCursor<'TRowKey,'TValue,'TResult[]>(
//          Func<'TRowKey,'TValue[],'TResult[]>(fun k vArr -> 
//            let vArrCopy = vArr |> Array.copy
//            sm.values <- vArrCopy 
//            let newMap = mapper.Invoke(sm)
//            let newArray = 
//              // TODO check if we have a sorted map as a result of mapper.Invoke
//              // and use its values directly
//              newMap.Values |> Seq.toArray
//            if Unchecked.equals newColumnKeys Unchecked.defaultof<'TColumnKey[]> then
//              newColumnKeys <- newMap.Keys |> Seq.toArray
//            else
//              if columnKeys.Length <> newArray.Length then invalidOp "RowWise series mappers returns series of different size"
//            newArray
//          ),
//          columns.Values |> Seq.map (fun s -> s.GetCursor) |> Seq.toArray)
//        :> ICursor<'TRowKey, 'TResult[]>
//
//    let rows = CursorSeries(Func<_>(zipCursor))
//    RowPanel(rows, columnKeys) :> Panel<'TRowKey,'TColumnKey, 'TResult>


  // TODO convenient ctors



and RowPanel<'TRowKey,'TColumnKey, 'TValue> (rows:Series<'TRowKey, 'TValue[]>, columnKeys:'TColumnKey[]) =
  inherit Panel<'TRowKey,'TColumnKey,'TValue>()
  let columnKeys = 
    // TODO check if they are sorted
    columnKeys
  let isIndexed = true

  // NB :) this is the only required method to get a functioning panel
  // Note from signatures that if we add Panel<K,C,V> to Series<K,V>, then: 
  // (Panel as Series<K, [inner series]>) + Series<K,V>) => for each K, we will have (inner Series<C,V> + V)
  // which is already supported by series.
  // The same is true for two panels, which will end up for each key as (inner Series<C,V> + other inner Series<C,V>)


  override this.GetCursor() : ICursor<'TRowKey, Series<'TColumnKey, 'TValue>> = 
    new MapValuesCursor<'TRowKey,'TValue[],Series<'TColumnKey, 'TValue>>(Func<_>(rows.GetCursor), (fun vArr -> 
      if isIndexed then
          let res = IndexedMap.OfSortedKeysAndValues(columnKeys, vArr) :> Series<'TColumnKey, 'TValue>
          // TODO IsMutable <- false
          res
        else
          let res = SortedMap.OfSortedKeysAndValues(columnKeys, vArr) :> Series<'TColumnKey, 'TValue>
          // TODO IsMutable <- false
          res
      ))
    :> ICursor<'TRowKey, Series<'TColumnKey, 'TValue>>

  // By being Series<>, Panel already implements IROOM for Rows

  override this.Columns with get() : Series<'TColumnKey, Series<'TRowKey,'TValue>> = raise (NotImplementedException("TODO"))
    //ColumnPanel(this :> IOrderedMap<'TColumnKey, Series<'TRowKey,'TValue>>)
  override this.Rows with get() = this :> Series<'TRowKey, Series<'TColumnKey,'TValue>>