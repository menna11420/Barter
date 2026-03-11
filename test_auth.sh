#!/bin/bash
EMAIL="testauth_$(date +%s)@gmail.com"
PASS="StrongPass123"

echo "Registering $EMAIL"
RES=$(curl -s -X POST http://localhost:5071/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Test User\",\"email\":\"$EMAIL\",\"password\":\"$PASS\"}")
echo $RES

echo "Logging in $EMAIL"
LOGIN_RES=$(curl -s -X POST http://localhost:5071/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}")
echo $LOGIN_RES
