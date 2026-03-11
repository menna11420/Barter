import 'package:flutter/material.dart';
import 'package:barter/core/resources/colors_manager.dart';
import 'package:barter/services/api_client.dart';

class SafeNetworkImage extends StatelessWidget {
  final String url;
  final BoxFit fit;
  final double? width;
  final double? height;

  const SafeNetworkImage({
    super.key,
    required this.url,
    this.fit = BoxFit.cover,
    this.width,
    this.height,
  });

  /// Converts a relative path like /uploads/x.jpg to a full URL like http://127.0.0.1:5071/uploads/x.jpg
  String get _resolvedUrl {
    if (url.isEmpty) return '';
    if (url.startsWith('http://') || url.startsWith('https://')) return url;
    // Relative path — prepend the API base (strip /api suffix)
    final base = ApiClient.baseUrl.replaceAll('/api', '');
    return '$base$url';
  }

  @override
  Widget build(BuildContext context) {
    final resolved = _resolvedUrl;

    if (resolved.isEmpty ||
        resolved.contains('via.placeholder.com') ||
        resolved.contains('dummyimage.com')) {
      return _placeholder(context);
    }

    return Image.network(
      resolved,
      fit: fit,
      width: width,
      height: height,
      errorBuilder: (context, error, stackTrace) => _placeholder(context),
      loadingBuilder: (context, child, loadingProgress) {
        if (loadingProgress == null) return child;
        return _placeholder(context);
      },
    );
  }

  Widget _placeholder(BuildContext context) {
    return Container(
      width: width,
      height: height,
      color: ColorsManager.shimmerBaseFor(context),
      child: Center(
        child: Icon(
          Icons.image_not_supported_rounded,
          color: ColorsManager.textSecondaryFor(context),
        ),
      ),
    );
  }
}
