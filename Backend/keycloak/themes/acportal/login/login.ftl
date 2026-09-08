<#import "template.ftl" as layout>
<@layout.registrationLayout>

<div class="login-card">

  <div class="acp-logo">
    
    <span class="logo-text">ACPortal</span>
  </div>

  <h2 class="acp-title">Get started now</h2>
  <p class="acp-subtitle">Enter your credentials below to access your account</p>

  <#if message?has_content>
    <div class="acp-alert acp-alert-${message.type}">
      ${kcSanitize(message.summary)?no_esc}
    </div>
  </#if>

  <form id="kc-form-login" action="${url.loginAction}" method="post">

    <div class="acp-field">
      <label for="username">Username</label>
      <div class="acp-input-wrap">
        <input id="username" name="username" type="text"
          class="acp-input"
          autofocus autocomplete="off"
          value="${login.username!''}"
          placeholder="Your Username" />
      </div>
    </div>

    <div class="acp-field">
      <label for="password">Password</label>
      <div class="acp-input-wrap">
        <input id="password" name="password" type="password"
          class="acp-input"
          autocomplete="off"
          placeholder="Your password"
          style="padding-right: 48px;" />
        <button type="button" class="toggle-pw" onclick="
          var i=document.getElementById('password');
          i.type=i.type==='password'?'text':'password';
          this.querySelector('svg').style.opacity=i.type==='text'?'1':'0.5'
        ">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
            <circle cx="12" cy="12" r="3"/>
          </svg>
        </button>
      </div>
    </div>

    <input type="hidden" name="credentialId"
      <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>/>

    <button type="submit" class="acp-btn">LOG IN</button>

  </form>

  <p class="acp-footer">ACPortal © Powered by ACPortal</p>

</div>

</@layout.registrationLayout>