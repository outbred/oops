# oops
First open source, cross-platform Do/Undo/Redo Framework that works in a user-driver application that's worth a dime.
Available on nuget [here](https://www.nuget.org/packages/oops/1.0.0)

## Premise

It is common for many user-facing applications to want some kind of undo/redo functionality for a user-driven action.  This framework should be cross-platform, configurable, and loose in its structure to allow for unforeseen usage requirements.  

## Definition and Goals

The difficulty in creating a general purpose undo/redo framework lies in tackling (at least) two core issues:

#### Being able to define a range of actions from a user as undoable, or making every single thing the user does undoable

- Making every character typed undoable, for example, can result in a klunky user experience, but hey it's a start.    
- The ideal framework would allow the code to aggregate changes together
- **oops** allows either configuration or somewhere in-between.  It's all about the scope of the Accumulator you 'new' up and use.
    
    
### Observing changes in all objects in the system in an ordered fashion for N unknown objects

For example, there may be 50 objects that are created and/or modified for any given user action and it would be tedious (at best) or impossible (for any real-world use-case) to funnel all changes into one undo action for the user by explicitly telling each object at the time of the change how to do it, and then how to undo it.
**oops** manages all of this for you by observing any and all property changes in a ViewModel (derived from TrackableViewModel) and any collection changes (TrackableCollection - list, stack, queue - or TrackableDictionary), and accumulating them into one undo action either globally or locally.
  
## Why distinguish between global and local changes?  Glad you asked.

Presume you have an application with a main window/form/page of some sort.  Anything that goes on in the app will observed and coalesced into undo actions through a menu-driven undo/redo system (like the back and forward arrows we're all used to).
   
Now, presume you have a dialog that pops up that does some undoable stuff, and also pops a different dialog that also does some undoable stuff. Depending on the project manager's whimsy, all of those actions from both dialogs should be aggregated into one undoable action for the user, or maybe there should be two.  Either scenario (and really any scenario I can think of) is available through **oops** with the option to create singleton/global scopes or local scopes, either of which can go onto the global undo stack.
 

## Components to support Undo/Redo for any ViewModel or Collection change

  * [x] **TrackableCollection** - a beefed up version of ObservableCollection:
    * Takes care of context switching to the UI thread for you (if it is set up to fire on the UI thread)
    * Seamlessly tracks all operations and makes them undoable - this can be turned on/off at any time
    * Fires CollectionChanged and PropertyChanged events like the UI needs, in the right order, on the right thread, and synchronously with the caller's thread - this can be turned off at any time
    * Handles concurrency just fine (hammer it from multiple threads and it'll all get sorted out)
    * Works as a Stack<> or a Queue<> as well (because sometimes you need those bound to your UI)
    * Highly performant (is that still a word?), as much as the UI will allow
    
  * [x] **TrackableDictionary** - super-charged version of Dictionary
    * Fires CollectionChanged and PropertyChanged events like the UI needs, in the right order, on the right thread, and synchronously with the caller's thread
    * Handles concurrency just fine (hammer it from multiple threads and it'll all get sorted out)
    * Takes care of context switching to the UI thread for you (if it is set up to fire on the UI thread)
    * Seamlessly tracks all operations and makes them undoable - this can be turned off at any time
    * Highly performant, as much as the UI will allow if it is bound to the UI
    
  * [x] **ConcurrentList**
    * A highly performant List<> that handles concurrency just fine
    * Has lots of helper methods like RemoveAndGetIndex, ReplaceAll, etc. that are inherently thread-safe
    * Also works as a Stack<> or a Queue<>
    
  * [x] **Accumulator**
    * Records all actions for undoing them later
    * Can be used a Singleton for application-scoped actions, or as an instance for locally-scoped actions (like in a dialog)
    
  * [x] **AccumulatorManager**
    * Manages the Undo and Redo stacks of accumulators
    * Auto-records redo operations for any undo, and vice-versa
    
  * [x] **TrackableViewModel**
    * Auto-tracks all changes to its properties in its referenced Accumulator (either the Singleton or a local one)
    * Changes are tracked by using custom Get<>/Set<> methods for any property change
      
  * [x] **TrackableScope**
    * Utility class to easily create an Accumulator and push it to the AccumulatorManager on Dispose
    * All changes within its 
    ```C# 
    using(new TrackableScope("testing"))
    ``` 
      block are aggregated into one Undo action for the User
      

## Demo app

  * See the oops.Demo WPF application
  
  
### Show me some :heart: and star this repo to support my project

### Created & Maintained By

[Brent Bulla](https://github.com/outbred) ([@outbred](https://www.twitter.com/outbred))
([Insta](https://www.instagram.com/outbred))

> If you found this project helpful or you learned something from the source code and want to thank me, consider buying me a cup of  <img src="https://vignette.wikia.nocookie.net/logopedia/images/a/ad/Dr._Pepper_1958.jpg/revision/latest?cb=20100924201743" height="25em" />  -  [PayPal](https://paypal.me/brentbulla/)
