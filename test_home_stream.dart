import 'package:flutter/material.dart';
import 'package:barter/services/api_service.dart';
import 'package:barter/model/item_model.dart';
import 'package:dio/dio.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    print("Testing API Service getItemsStream...");
    final items = await ApiService.getItemsStream().first;
    print("Successfully fetched \${items.length} items.");
  } catch (e, stack) {
    print("=== CRASH === \\n\$e\\n\$stack");
  }
}
