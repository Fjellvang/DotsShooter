import{aa as i}from"./index-B7jvAjdE.js";function n(e,r){return!(e.$type!==r.$type||e.matcher&&e.matcher(r)!==!0)}function s(e){let t=i().gameSpecific.playerRewards.find(a=>n(a,e));if(!t){console.error(`Unregistered reward type: ${e.$type}. Did you register it via the integration API?`);const a=`Unregistered Type: ${e.$type}`;t={getDisplayValue:()=>a,$type:e.$type}}return{...e,...t}}function p(e){return e.map(r=>s(r))}export{s as a,p as r};
