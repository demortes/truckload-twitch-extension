/**
 * Services Index
 *
 * Export all services for clean imports:
 * import { truckyApi, telemetryService } from './services';
 */

export { default as truckyApi } from './truckyApi';
export { default as telemetryService } from './telemetryService';

// Named exports for direct function imports
export * from './truckyApi';
export * from './telemetryService';
