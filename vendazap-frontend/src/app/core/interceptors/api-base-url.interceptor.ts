import { HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export const apiBaseUrlInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  if (req.url.startsWith('http')) {
    return next(req);
  }

  const apiReq = req.clone({ url: `${environment.apiBaseUrl}${req.url}` });
  return next(apiReq);
};
