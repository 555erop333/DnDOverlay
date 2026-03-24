using System;
using System.Collections.Generic;
using System.Linq;
using LibVLCSharp.Shared;

namespace DnDOverlay.Infrastructure
{
    internal sealed class LibVlcContext : IDisposable
    {
        private readonly LibVLC _libVlc;
        private bool _disposed;

        private LibVlcContext(LibVLC libVlc)
        {
            _libVlc = libVlc ?? throw new ArgumentNullException(nameof(libVlc));
        }

        public static LibVlcContext Create()
        {
            Core.Initialize();

            var options = new[]
            {
                "--no-xlib",
                "--quiet",
                "--extraintf=",
                "--intf=dummy",
                "--ignore-config",
                "--no-osd",
                "--network-caching=100",
                "--http-reconnect"
            };

            var libVlc = new LibVLC(options);
            return new LibVlcContext(libVlc);
        }

        public LibVLC LibVlc => _libVlc;

        public MediaPlayer CreateMediaPlayer()
        {
            ThrowIfDisposed();
            return new MediaPlayer(_libVlc);
        }

        public Media CreateMedia(string mrl, IEnumerable<string>? options = null)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(mrl))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(mrl));
            }

            var media = new Media(_libVlc, mrl, FromType.FromLocation);
            ApplyOptions(media, options);
            return media;
        }

        public Media CreateMedia(Uri mrl, IEnumerable<string>? options = null)
        {
            if (mrl == null)
            {
                throw new ArgumentNullException(nameof(mrl));
            }

            return CreateMedia(mrl.ToString(), options);
        }

        public static string BuildHttpHeaderOption(string headerName, string headerValue)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                throw new ArgumentException("Header name cannot be null or whitespace.", nameof(headerName));
            }

            headerValue ??= string.Empty;
            return $":http-header={headerName}: {headerValue}";
        }

        public static string BuildStartTimeOption(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            {
                seconds = 0;
            }

            return $":start-time={seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        private static void ApplyOptions(Media media, IEnumerable<string>? options)
        {
            if (options == null)
            {
                return;
            }

            foreach (var option in options.Where(o => !string.IsNullOrWhiteSpace(o)))
            {
                media.AddOption(option);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _libVlc.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LibVlcContext));
            }
        }
    }
}
