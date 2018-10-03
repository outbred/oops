# DURF
First open source, cross-platform Do/Undo/Redo Framework that works in a user-driver application that's worth a dime.

## Features

* [x] Undo/Redo for any ViewModel or Collection change

  * [x] **TrackableCollection** - a beefed up version of ObservableCollection:
    * [x] Takes care of context switching to the UI thread for you (if it is set up to fire on the UI thread)
    * [x] Seamlessly tracks all operations and makes them undoable - this can be turned on/off at any time
    * [x] Fires CollectionChanged and PropertyChanged events like the UI needs, in the right order, on the right thread, and synchronously with the caller's thread - this can be turned off at any time
    * [x] Handles concurrency just fine (hammer it from multiple threads and it'll all get sorted out)
    * [x] Works as a Stack<> or a Queue<> as well (because sometimes you need those bound to your UI)
    * [x] Highly performant (is that still a word?), as much as the UI will allow
  * [x] **TrackableDictionary** - super-charged version of Dictionary
    * [x] Fires CollectionChanged and PropertyChanged events like the UI needs, in the right order, on the right thread, and synchronously with the caller's thread
    * [x] Handles concurrency just fine (hammer it from multiple threads and it'll all get sorted out)
    * [x] Takes care of context switching to the UI thread for you (if it is set up to fire on the UI thread)
    * [x] Seamlessly tracks all operations and makes them undoable - this can be turned off at any time
    * [x] Highly performant, as much as the UI will allow if it is bound to the UI
  * [x] **ConcurrentList**
    * [x] A highly performant List<> that handles concurrency just fine
    * [x] has lots of helper methods like RemoveAndGetIndex, ReplaceAll, etc. that are inherently thread-safe
    * [x] Also works as a Stack<> or a Queue<>
  * [x] **Accumulator**
    * [x] Records all actions for undoing them later
    * [x] Can be used a Singleton for application-scoped actions, or as an instance for locally-scoped actions (like in a dialog)
  * [x] **AccumulatorManager**
    * [x] Manages the Undo and Redo stacks of accumulators
    * [x] Auto-records redo operations for any undo, and vice-versa
  * [x] **TrackableViewModel**
    * [x] Auto-tracks all changes to its properties in its referenced Accumulator (either the Singleton or a local one)
      * This is performed by using custom Get<>/Set<> methods for any property change
      

## Demo app

  * See the DURF.Demo WPF application
  
  

### Show me some :heart: and star this repo to support my project

# Pull Requests

I welcome and encourage all pull requests. It usually will take me within 24-72 hours to respond to any issue or request. Here are some basic rules to follow to ensure timely addition of your request:

1.  Match coding style (braces, spacing, etc.), or I will do it for you
2.  If its a feature, bugfix, or anything please only change code to what you specify.
3.  Please keep PR titles easy to read and descriptive of changes, this will make them easier to merge :)
4.  Pull requests _must_ be made against `develop` branch. Any other branch (unless specified by the maintainers) will get rejected.
5.  Check for existing [issues](https://github.com/outbred/DURF/issues) first, before filing an issue.
6.  Make sure you follow the set standard as all other projects in this repo do

### Created & Maintained By

[Brent Bulla](https://github.com/outbred) ([@outbred](https://www.twitter.com/outbred))
([Insta](https://www.instagram.com/outbred))

> If you found this project helpful or you learned something from the source code and want to thank me, consider buying me a cup of  <img src="https://vignette.wikia.nocookie.net/logopedia/images/a/ad/Dr._Pepper_1958.jpg/revision/latest?cb=20100924201743" height="25em" />
>
> * [PayPal](https://www.paypal.me/outbred/)

# License
MIT License

Copyright (c) 2018 Brent Bulla

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Getting Started

TODO
