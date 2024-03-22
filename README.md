# Unity Light Ranking
This project contains a set of Unity scripts that can be used to rank the lights in a scene based on prevelance in the scene. Prevelance in this case is calculated based on the average greyscale value across a set of frames of an expected use-case.

## Features

### Object Identification
The Identifier script allows us to uniquely identify each object in the scene consistently between runs. The Unity object name string cannot be used as a unique identifier since two objects can have the same name. Unity does provide a unique identifier for objects, but it is not guaranteed to be consistent from run-to-run. Having run-to-run consistency is important for being able to capture a use-case trace and replay it across executions of the application.

### Trace Capture

### Trace Replay

### Light Iteration

## Installation

## Usage
