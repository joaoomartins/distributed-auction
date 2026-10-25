# Distributed Auction — Client/Server Application in C#

![C#](https://img.shields.io/badge/C%23-.NET-512BD4?logo=dotnet&logoColor=white)
![TCP](https://img.shields.io/badge/Protocol-TCP%20Sockets-blue)
![Concurrency](https://img.shields.io/badge/Concurrency-Multi--threaded-orange)
![Status](https://img.shields.io/badge/Status-Academic%20Project-success)

**Course:** Distributed Systems
**Programme:** Computer Engineering — Polytechnic University of Portalegre (ESTG)
**Assignment:** No. 1
**Academic Year:** 2026/2027
**Author(s):** `João Martins nº22999` and `Samuel Ventura nº24323`

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Objectives](#2-objectives)
3. [System Overview](#3-system-overview)
4. [Communication Protocol](#4-communication-protocol)
5. [Synchronisation of Threads](#5-synchronisation-of-threads)
6. [How the Auction Works](#6-how-the-auction-works)
7. [Input Validation and Error Handling](#7-input-validation-and-error-handling)
8. [Project Structure](#8-project-structure)
9. [Requirements and Execution](#9-requirements-and-execution)
10. [Usage Example](#10-usage-example)
11. [Limitations](#11-limitations)
12. [Conclusion](#12-conclusion)

---

## 1. Introduction

This project was developed for the Distributed Systems course and consists of an auction application written in C#. The application follows the client/server model and uses TCP sockets for communication. A server hosts the auction of one item, and several clients (the bidders) connect to it, register with a username and place bids until the auction ends.

## 2. Objectives

The main objectives of this assignment are:

- to use TCP sockets to enable communication between a server and several clients;
- to define a simple text-based protocol for the messages exchanged;
- to serve several clients at the same time using threads on the server;
- to protect data shared between threads, avoiding race conditions;
- to control time-related events (admission period and auction duration);
- to validate user input and handle communication errors.

## 3. System Overview

The system is composed of two applications: a **server** and a **client**. The server holds all the auction information (item, base price, highest offer and highest bidder) and is the only entity that decides whether a bid is valid. The clients only send commands and display the responses received.

When the server starts, its main thread accepts incoming connections. For each client that connects, the server creates a new thread dedicated to that client. This way, all bidders can be served simultaneously.

The source code is organised in four files:

| File | Description |
|---|---|
| `Server.cs` | Reads the auction configuration, accepts clients during the admission period, creates one thread per client, controls the auction time and announces the result. |
| `ServerThread.cs` | Code executed by each thread of the server. It registers the username and processes the commands sent by one client. |
| `Client.cs` | Console application used by the bidder to connect to the server and send commands. |
| `InputValidation.cs` | Auxiliary class used to validate numbers typed in the console. |

## 4. Communication Protocol

Client and server exchange text messages, one per line. In the messages sent by the server, the fields are separated by a semicolon (`;`). For this reason, usernames cannot contain this character.

### 4.1 Registration

| Sent by | Message | Meaning |
|---|---|---|
| Client | `<username>` | The client asks to register with this name. |
| Server | `NAME_OK` | The username was accepted. |
| Server | `INVALID_NAME;<reason>` | The name is empty or contains `;`. |
| Server | `Duplicated name` | The username is already being used by another client. |
| Server | `ADMISSION_CLOSED` | The admission period is over and the connection is closed. |

### 4.2 Start of the Auction

| Sent by | Message | Meaning |
|---|---|---|
| Server | `AUCTION_START;<item>;<base_price>;<duration>` | Sent to every registered client when the admission period ends. |

### 4.3 During the Auction

| Sent by | Message | Meaning |
|---|---|---|
| Client | `OFFER <value>` | The client places a bid. |
| Client | `STATE` | The client asks for the current state of the auction. |
| Client | `Exit` | The client leaves the auction. |
| Server | `OFFER_OK;<value>;<highest_offer>` | The bid was accepted. |
| Server | `OFFER_REJECTED;<reason>;<highest_offer>` | The bid was rejected. |
| Server | `STATE_INFO;<item>;<base_price>;<highest_offer>;<bidder>` | Current state of the auction. |
| Server | `AUCTION_ENDED;WINNER;<bidder>;<value>` | The auction ended and the item was sold. |
| Server | `AUCTION_ENDED;NO_WINNER;0` | The auction ended and the item was not sold. |
| Server | `UNKNOWN_COMMAND;<help>` | The command was not recognised. |

### 4.4 Rules for Accepting a Bid

A bid is rejected by the server if:

1. it does not have exactly one value (e.g. `OFFER 250`);
2. the value is not a valid number;
3. the value is zero or negative;
4. the auction has already ended;
5. the value is lower than the base price;
6. the value is not higher than the current highest offer.

The decimal comma is accepted and converted to a decimal point, and parsing uses `CultureInfo.InvariantCulture` so that it does not depend on the computer's regional settings.

## 5. Synchronisation of Threads

Since all the client threads read and modify the same data (highest offer, highest bidder and list of usernames), it was necessary to synchronise their access:

| Mechanism | Purpose |
|---|---|
| `Mutex` | Ensures that only one thread at a time accesses the shared data, so two bids cannot be processed at the same time. It is always released in a `finally` block. |
| `SemaphoreSlim` | Keeps the client threads waiting after registration until the admission period ends, so the auction starts for everyone at the same time. |
| `volatile` variables | `ADMISSION_OPEN` and `AUCTION_ENDED` are marked as `volatile` so that every thread sees their most recent value. |
| `CancellationTokenSource` | Cancels the acceptance of new connections after the admission time has passed. |
| `Thread.Join` with timeout | Allows the server to wait for the clients only during the maximum duration of the auction. |

## 6. How the Auction Works

1. **Configuration** — The server operator enters the item name, base price, maximum duration, admission time and port number (between 1024 and 65535).
2. **Admission period** — The server accepts connections and registers the clients. Clients trying to join afterwards receive `ADMISSION_CLOSED`.
3. **Start** — When the admission period ends, the server stops accepting connections and releases the client threads. If nobody registered, the auction is cancelled.
4. **Bidding** — Clients place bids and request the auction state until the time is over.
5. **Result** — The server marks the auction as ended and determines the winner, who is the bidder with the highest offer, provided it is not lower than the base price.
6. **End** — The server waits for all clients to type `Exit` and then shuts down.

## 7. Input Validation and Error Handling

- **Server configuration** — The `InputValidation` class asks again until a valid value is typed (positive integers, valid port, positive number for the price).
- **Username** — Empty names, names with `;` and repeated names are rejected.
- **Connection errors** — The client handles `SocketException` and `IOException`. On the server, each thread handles its own errors, removes the username and closes the socket.
- **Client disconnection** — When the server reads `null` from a client, it considers that the client has disconnected and ends the corresponding thread.
- **Commands** — Commands are not case-sensitive. If the client presses Enter without typing anything, the `STATE` command is sent.

## 8. Project Structure

```
LeilaoDistribuido/
├── Shared/
│   └── InputValidation.cs
├── Server/
│   ├── Server.cs
│   └── ServerThread.cs
├── Client/
│   └── Client.cs
└── README.md
```

> The folder organisation may differ depending on the Visual Studio solution. The namespaces used are `Shared`, `LeilaoDistribuido.Server` and `LeilaoDistribuido.Client`.

## 9. Requirements and Execution

### 9.1 Requirements

- .NET SDK 6.0 or later;
- Visual Studio 2022 (or the `dotnet` command-line tool).

### 9.2 Running

1. Start the server and enter the requested data (item name, base price, duration, admission time and port).
2. Start one or more clients (one console per client) and enter the server hostname, the port and a username.
3. After the admission period ends, use the commands `OFFER <value>`, `STATE` and `Exit`.

Using the command line:

```bash
dotnet run --project Server
dotnet run --project Client
```

## 10. Usage Example

```text
Command > STATE

--- CURRENT AUCTION STATE ---
Item: Watch
Base price: 100 EUR
Highest offer: No offers yet
Current offerer: None
-----------------------------

Command > OFFER 150
[OFFER_SUCCESS] Your offer of 150 EUR was accepted! Current highest: 150 EUR.

Command > OFFER 120
[OFFER_REJECTED] Offer must be higher than current highest offer of 150 EUR | Current highest: 150 EUR
```

## 11. Limitations

- The client only finds out that the auction has ended when it sends a new command, because the server only replies to client requests.
- The server uses one thread per client, which is adequate for this assignment but not for a very large number of clients.
- Messages are sent in plain text, without encryption or authentication.
- The server manages only one item per execution and does not store the results.
- The method `ValidateDoubleGT0` accepts the value 0 for the base price and should only accept values greater than 0.
- The message `Duplicated name` does not follow the same format (`CODE;message`) as the other server messages.

## 12. Conclusion

This assignment allowed the application of the main concepts studied in Distributed Systems: communication through TCP sockets, definition of a protocol, handling of several clients with threads and synchronisation of shared data. The final application works as an auction with an admission period, concurrent bids and the determination of a winner at the end of the established time.

---

*Assignment developed for the Distributed Systems course — Polytechnic University of Portalegre.*
