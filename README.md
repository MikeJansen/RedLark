# Prototype Distributed Lock Manager

Written in C# and based on the [Redis Redlock](https://redis.com/redis-best-practices/communication-patterns/redlock/) pattern, 
this is a Redis-based distributed lock manager (DLM).

The main improvement over the original pattern is to parallelize requests to the multi-masters and to short circuit on waiting 
for a response once a quorum is reached.

This project is "experimental", "in progress", "not fully tested", etc.
